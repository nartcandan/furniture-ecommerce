using FluentValidation;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Domain.Entities;
using NSubstitute;

namespace FurnitureEcommerce.UnitTests.Carts;

public class CartServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly ICartRepository repository = Substitute.For<ICartRepository>();
    private readonly IProductRepository productRepository = Substitute.For<IProductRepository>();
    private readonly IFileStorage fileStorage = Substitute.For<IFileStorage>();
    private readonly CartService service;

    public CartServiceTests()
    {
        service = new CartService(
            repository,
            productRepository,
            fileStorage,
            new AddCartItemRequestValidator(),
            new UpdateCartItemRequestValidator());

        fileStorage.GetUrl(Arg.Any<string>()).Returns(call => $"http://files/{call.Arg<string>()}");
    }

    private static Product NewProduct(
        string name = "Oslo Sofa",
        decimal price = 1000,
        int stock = 10,
        bool isActive = true,
        bool isCategoryActive = true)
    {
        return new Product
        {
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Price = price,
            StockQuantity = stock,
            IsActive = isActive,
            Category = new Category { Name = "Sofas", Slug = "sofas", IsActive = isCategoryActive }
        };
    }

    private void CartContains(params (Product Product, int Quantity)[] lines)
    {
        var items = lines
            .Select(line => new CartItem
            {
                Product = line.Product,
                ProductId = line.Product.Id,
                Quantity = line.Quantity
            })
            .ToList();

        repository.GetItemsAsync(UserId, Arg.Any<CancellationToken>()).Returns(items);
    }

    [Fact]
    public async Task GetAsync_ReturnsEmptyCart_ThatCannotCheckOut()
    {
        CartContains();

        var cart = await service.GetAsync(UserId, CancellationToken.None);

        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.TotalQuantity);
        Assert.Equal(0, cart.TotalAmount);
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public async Task GetAsync_CalculatesLineTotalsAndCartTotals_FromCurrentPrices()
    {
        CartContains((NewProduct("Oslo Sofa", price: 1000), 2), (NewProduct("Malmo Lamp", price: 249.90m), 3));

        var cart = await service.GetAsync(UserId, CancellationToken.None);

        Assert.Equal([2000m, 749.70m], cart.Items.Select(item => item.LineTotal));
        Assert.Equal(5, cart.TotalQuantity);
        Assert.Equal(2749.70m, cart.TotalAmount);
        Assert.True(cart.CanCheckout);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task GetAsync_MarksLineUnavailable_WhenProductOrCategoryIsInactive(
        bool isActive,
        bool isCategoryActive)
    {
        CartContains(
            (NewProduct("Oslo Sofa"), 1),
            (NewProduct("Malmo Lamp", isActive: isActive, isCategoryActive: isCategoryActive), 1));

        var cart = await service.GetAsync(UserId, CancellationToken.None);

        Assert.Equal([true, false], cart.Items.Select(item => item.IsAvailable));
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public async Task GetAsync_MarksLine_WhenStockIsBelowQuantity()
    {
        CartContains((NewProduct("Oslo Sofa", stock: 5), 5), (NewProduct("Malmo Lamp", stock: 2), 3));

        var cart = await service.GetAsync(UserId, CancellationToken.None);

        Assert.Equal([false, true], cart.Items.Select(item => item.HasInsufficientStock));
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public async Task GetAsync_ReturnsMainImageUrl_OrNullWithoutImages()
    {
        var withImages = NewProduct("Oslo Sofa");
        withImages.Images.Add(new ProductImage { ImagePath = "products/second.png", SortOrder = 2 });
        withImages.Images.Add(new ProductImage { ImagePath = "products/main.png", SortOrder = 1 });
        CartContains((withImages, 1), (NewProduct("Malmo Lamp"), 1));

        var cart = await service.GetAsync(UserId, CancellationToken.None);

        Assert.Equal(["http://files/products/main.png", null], cart.Items.Select(item => item.ImageUrl));
    }

    [Fact]
    public async Task AddItemAsync_ThrowsNotFound_WhenProductCannotBeSold()
    {
        var productId = Guid.NewGuid();
        productRepository.GetActiveByIdAsync(productId, Arg.Any<CancellationToken>()).Returns((Product?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddItemAsync(UserId, new AddCartItemRequest(productId, 1), CancellationToken.None));

        await repository.DidNotReceiveWithAnyArgs().TryAddItemAsync(default, default, default, default);
    }

    [Fact]
    public async Task AddItemAsync_ThrowsConflict_WhenStockIsNotEnough()
    {
        var product = NewProduct(stock: 2);
        productRepository.GetActiveByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.TryAddItemAsync(UserId, product.Id, 3, Arg.Any<CancellationToken>()).Returns(false);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddItemAsync(UserId, new AddCartItemRequest(product.Id, 3), CancellationToken.None));

        Assert.Contains("Only 2 available", exception.Message);
    }

    [Fact]
    public async Task AddItemAsync_ReturnsCart_WhenItemIsAdded()
    {
        var product = NewProduct();
        productRepository.GetActiveByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.TryAddItemAsync(UserId, product.Id, 2, Arg.Any<CancellationToken>()).Returns(true);
        CartContains((product, 2));

        var cart = await service.AddItemAsync(UserId, new AddCartItemRequest(product.Id, 2), CancellationToken.None);

        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task AddItemAsync_ThrowsValidation_BeforeTouchingTheCart()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddItemAsync(UserId, new AddCartItemRequest(Guid.NewGuid(), 0), CancellationToken.None));

        await productRepository.DidNotReceiveWithAnyArgs().GetActiveByIdAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().TryAddItemAsync(default, default, default, default);
    }

    [Fact]
    public async Task SetItemQuantityAsync_ThrowsNotFound_WhenProductIsNotInCart()
    {
        var productId = Guid.NewGuid();
        repository.TrySetItemQuantityAsync(UserId, productId, 2, Arg.Any<CancellationToken>()).Returns(false);
        repository.GetItemAsync(UserId, productId, Arg.Any<CancellationToken>()).Returns((CartItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SetItemQuantityAsync(UserId, productId, new UpdateCartItemRequest(2), CancellationToken.None));
    }

    [Fact]
    public async Task SetItemQuantityAsync_ThrowsConflict_WhenStockIsNotEnough()
    {
        var product = NewProduct(stock: 4);
        var item = new CartItem { Product = product, ProductId = product.Id, Quantity = 1 };
        repository.TrySetItemQuantityAsync(UserId, product.Id, 5, Arg.Any<CancellationToken>()).Returns(false);
        repository.GetItemAsync(UserId, product.Id, Arg.Any<CancellationToken>()).Returns(item);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetItemQuantityAsync(UserId, product.Id, new UpdateCartItemRequest(5), CancellationToken.None));

        Assert.Contains("Only 4 available", exception.Message);
    }

    [Fact]
    public async Task SetItemQuantityAsync_ThrowsValidation_BeforeTouchingTheCart()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SetItemQuantityAsync(
                UserId, Guid.NewGuid(), new UpdateCartItemRequest(0), CancellationToken.None));

        await repository.DidNotReceiveWithAnyArgs().TrySetItemQuantityAsync(default, default, default, default);
    }

    [Fact]
    public async Task RemoveItemAsync_ThrowsNotFound_WhenProductIsNotInCart()
    {
        var productId = Guid.NewGuid();
        repository.RemoveItemAsync(UserId, productId, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RemoveItemAsync(UserId, productId, CancellationToken.None));
    }
}
