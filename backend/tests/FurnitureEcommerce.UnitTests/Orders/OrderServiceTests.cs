using FluentValidation;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Domain.Entities;
using NSubstitute;

namespace FurnitureEcommerce.UnitTests.Orders;

public class OrderServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IOrderRepository repository = Substitute.For<IOrderRepository>();
    private readonly ICartRepository cartRepository = Substitute.For<ICartRepository>();
    private readonly IAddressRepository addressRepository = Substitute.For<IAddressRepository>();
    private readonly OrderService service;

    private readonly Address address = new()
    {
        UserId = UserId,
        Title = "Home",
        RecipientFirstName = "Ayse",
        RecipientLastName = "Yilmaz",
        PhoneNumber = "+905551112233",
        City = "Istanbul",
        District = "Kadikoy",
        AddressLine = "Moda Cd. No: 1",
        PostalCode = "34710"
    };

    public OrderServiceTests()
    {
        service = new OrderService(repository, cartRepository, addressRepository, new CreateOrderRequestValidator());

        addressRepository.GetForUserAsync(address.Id, UserId, Arg.Any<CancellationToken>()).Returns(address);
        repository.CreateAndReserveStockAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);
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

        cartRepository.GetItemsAsync(UserId, Arg.Any<CancellationToken>()).Returns(items);
    }

    private Task<OrderResponse> CreateAsync() =>
        service.CreateAsync(UserId, new CreateOrderRequest(address.Id), CancellationToken.None);

    private Task AssertNothingSavedAsync() =>
        repository.DidNotReceiveWithAnyArgs().CreateAndReserveStockAsync(default!, default);

    [Fact]
    public async Task CreateAsync_CopiesAddressNamesAndPrices_AndWaitsForPayment()
    {
        var sofa = NewProduct("Oslo Sofa", price: 1000);
        var lamp = NewProduct("Malmo Lamp", price: 249.90m);
        CartContains((sofa, 2), (lamp, 3));

        var order = await CreateAsync();

        Assert.Equal("PendingPayment", order.Status);
        Assert.Equal(2749.70m, order.TotalAmount);
        Assert.Equal("Ayse", order.RecipientFirstName);
        Assert.Equal("Moda Cd. No: 1", order.AddressLine);
        Assert.Equal("34710", order.PostalCode);
        Assert.Equal(
            [
                new OrderItemResponse(sofa.Id, "Oslo Sofa", 1000, 2, 2000),
                new OrderItemResponse(lamp.Id, "Malmo Lamp", 249.90m, 3, 749.70m)
            ],
            order.Items.OrderByDescending(item => item.UnitPrice));
        await repository.Received(1).CreateAndReserveStockAsync(
            Arg.Is<Order>(saved => saved.UserId == UserId && saved.Status == OrderStatus.PendingPayment),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotFound_WhenAddressIsNotTheUsers()
    {
        CartContains((NewProduct(), 1));
        var otherAddressId = Guid.NewGuid();
        addressRepository.GetForUserAsync(otherAddressId, UserId, Arg.Any<CancellationToken>())
            .Returns((Address?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(UserId, new CreateOrderRequest(otherAddressId), CancellationToken.None));

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task CreateAsync_ThrowsConflict_WhenCartIsEmpty()
    {
        CartContains();

        await Assert.ThrowsAsync<ConflictException>(CreateAsync);

        await AssertNothingSavedAsync();
    }

    [Theory]
    [InlineData(false, true, 10)]
    [InlineData(true, false, 10)]
    [InlineData(true, true, 1)]
    public async Task CreateAsync_ThrowsConflict_WhenAnItemCannotBeOrdered(
        bool isActive,
        bool isCategoryActive,
        int stock)
    {
        CartContains(
            (NewProduct("Oslo Sofa"), 1),
            (NewProduct("Malmo Lamp", stock: stock, isActive: isActive, isCategoryActive: isCategoryActive), 2));

        var exception = await Assert.ThrowsAsync<ConflictException>(CreateAsync);

        Assert.Contains("Malmo Lamp", exception.Message);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task CreateAsync_ThrowsConflict_WhenStockIsTakenWhileSaving()
    {
        var sofa = NewProduct("Oslo Sofa");
        var lamp = NewProduct("Malmo Lamp");
        CartContains((sofa, 1), (lamp, 1));
        repository.CreateAndReserveStockAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(lamp.Id);

        var exception = await Assert.ThrowsAsync<ConflictException>(CreateAsync);

        Assert.Contains("Malmo Lamp", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_ThrowsValidation_ForMissingAddressId()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(UserId, new CreateOrderRequest(Guid.Empty), CancellationToken.None));

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFound_WhenOrderIsNotTheUsers()
    {
        var orderId = Guid.NewGuid();
        repository.GetForUserAsync(orderId, UserId, Arg.Any<CancellationToken>()).Returns((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetByIdAsync(UserId, orderId, CancellationToken.None));
    }
}
