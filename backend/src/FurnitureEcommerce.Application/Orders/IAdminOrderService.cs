using FurnitureEcommerce.Application.Common;

namespace FurnitureEcommerce.Application.Orders;

public interface IAdminOrderService
{
    /// <summary>Returns the orders of all customers, newest first.</summary>
    Task<PagedResult<AdminOrderSummaryResponse>> GetPagedAsync(
        AdminOrderQuery query,
        CancellationToken cancellationToken);

    Task<AdminOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Moves a Paid order to Shipped.</summary>
    Task<AdminOrderResponse> ShipAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Moves a Shipped order to Delivered.</summary>
    Task<AdminOrderResponse> DeliverAsync(Guid id, CancellationToken cancellationToken);
}
