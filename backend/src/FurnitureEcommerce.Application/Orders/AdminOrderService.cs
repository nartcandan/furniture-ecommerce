using FluentValidation;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Orders;

public class AdminOrderService(
    IOrderRepository repository,
    IValidator<AdminOrderQuery> queryValidator) : IAdminOrderService
{
    public async Task<PagedResult<AdminOrderSummaryResponse>> GetPagedAsync(
        AdminOrderQuery query,
        CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(query, cancellationToken);

        OrderStatus? status = string.IsNullOrWhiteSpace(query.Status)
            ? null
            : Enum.Parse<OrderStatus>(query.Status, ignoreCase: true);

        var orders = await repository.GetPagedAsync(status, query.Page, query.PageSize, cancellationToken);

        return new PagedResult<AdminOrderSummaryResponse>(
            orders.Items
                .Select(order => new AdminOrderSummaryResponse(
                    order.Id,
                    order.Status.ToString(),
                    order.TotalAmount,
                    order.Items.Sum(item => item.Quantity),
                    $"{order.RecipientFirstName} {order.RecipientLastName}",
                    order.City,
                    order.PaidAt,
                    order.CreatedAt))
                .ToList(),
            orders.Page,
            orders.PageSize,
            orders.TotalCount);
    }

    public async Task<AdminOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return ToResponse(await GetExistingAsync(id, cancellationToken));
    }

    public Task<AdminOrderResponse> ShipAsync(Guid id, CancellationToken cancellationToken)
    {
        // Only from Paid. An order that is being cancelled is in Cancelling, so it cannot be shipped.
        return ChangeStatusAsync(id, OrderStatus.Paid, OrderStatus.Shipped, cancellationToken);
    }

    public Task<AdminOrderResponse> DeliverAsync(Guid id, CancellationToken cancellationToken)
    {
        return ChangeStatusAsync(id, OrderStatus.Shipped, OrderStatus.Delivered, cancellationToken);
    }

    private async Task<AdminOrderResponse> ChangeStatusAsync(
        Guid id,
        OrderStatus from,
        OrderStatus to,
        CancellationToken cancellationToken)
    {
        // The expected status is part of the update itself, so a customer's cancellation and
        // an admin's shipment cannot both succeed for the same order.
        if (!await repository.TryChangeStatusAsync(id, from, to, cancellationToken))
        {
            var current = await GetExistingAsync(id, cancellationToken);

            throw new ConflictException(
                $"Only an order in status {from} can be set to {to}. This order is {current.Status}.");
        }

        return ToResponse(await GetExistingAsync(id, cancellationToken));
    }

    private async Task<Order> GetExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Order '{id}' was not found.");
    }

    private static AdminOrderResponse ToResponse(Order order) => new(
        order.Id,
        order.UserId,
        order.Status.ToString(),
        order.TotalAmount,
        order.RecipientFirstName,
        order.RecipientLastName,
        order.PhoneNumber,
        order.City,
        order.District,
        order.AddressLine,
        order.PostalCode,
        order.PaymentId,
        order.PaymentError,
        order.PaidAt,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items
            .OrderBy(item => item.ProductName, StringComparer.Ordinal)
            .ThenBy(item => item.ProductId)
            .Select(item => new OrderItemResponse(
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.UnitPrice * item.Quantity))
            .ToList());
}
