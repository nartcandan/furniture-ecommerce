using FluentValidation;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Domain.Entities;
using NSubstitute;

namespace FurnitureEcommerce.UnitTests.Orders;

public class AdminOrderServiceTests
{
    private readonly IOrderRepository repository = Substitute.For<IOrderRepository>();
    private readonly AdminOrderService service;

    public AdminOrderServiceTests()
    {
        service = new AdminOrderService(repository, new AdminOrderQueryValidator());

        repository
            .GetPagedAsync(Arg.Any<OrderStatus?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([], 1, 20, 0));
    }

    private Order OrderIs(OrderStatus status)
    {
        var order = new Order
        {
            Status = status,
            RecipientFirstName = "Ayse",
            RecipientLastName = "Yilmaz",
            PhoneNumber = "+905551112233",
            City = "Istanbul",
            District = "Kadikoy",
            AddressLine = "Moda Cd. No: 1"
        };

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        return order;
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Paid", OrderStatus.Paid)]
    [InlineData("shipped", OrderStatus.Shipped)]
    public async Task GetPagedAsync_PassesStatusFilter(string? status, OrderStatus? expected)
    {
        await service.GetPagedAsync(new AdminOrderQuery { Status = status }, CancellationToken.None);

        await repository.Received(1).GetPagedAsync(expected, 1, 20, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Unknown", 1, 20)]
    [InlineData("3", 1, 20)]
    [InlineData(null, 0, 20)]
    [InlineData(null, 1, 0)]
    [InlineData(null, 1, 101)]
    public async Task GetPagedAsync_ThrowsValidation_ForInvalidQuery(string? status, int page, int pageSize)
    {
        var query = new AdminOrderQuery { Status = status, Page = page, PageSize = pageSize };

        await Assert.ThrowsAsync<ValidationException>(() => service.GetPagedAsync(query, CancellationToken.None));
    }

    [Fact]
    public async Task ShipAsync_ChangesOnlyFromPaid()
    {
        var order = OrderIs(OrderStatus.Paid);
        repository
            .TryChangeStatusAsync(order.Id, OrderStatus.Paid, OrderStatus.Shipped, Arg.Any<CancellationToken>())
            .Returns(true);

        await service.ShipAsync(order.Id, CancellationToken.None);

        await repository.Received(1).TryChangeStatusAsync(
            order.Id, OrderStatus.Paid, OrderStatus.Shipped, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliverAsync_ChangesOnlyFromShipped()
    {
        var order = OrderIs(OrderStatus.Shipped);
        repository
            .TryChangeStatusAsync(order.Id, OrderStatus.Shipped, OrderStatus.Delivered, Arg.Any<CancellationToken>())
            .Returns(true);

        await service.DeliverAsync(order.Id, CancellationToken.None);

        await repository.Received(1).TryChangeStatusAsync(
            order.Id, OrderStatus.Shipped, OrderStatus.Delivered, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShipAsync_ThrowsConflict_NamingTheCurrentStatus_WhenOrderIsNotPaid()
    {
        var order = OrderIs(OrderStatus.Cancelling);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ShipAsync(order.Id, CancellationToken.None));

        Assert.Contains("Cancelling", exception.Message);
    }

    [Fact]
    public async Task ShipAsync_ThrowsNotFound_ForMissingOrder()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ShipAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
