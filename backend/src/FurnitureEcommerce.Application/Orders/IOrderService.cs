namespace FurnitureEcommerce.Application.Orders;

public interface IOrderService
{
    /// <param name="ipAddress">The customer's address as seen by the API, passed on to the payment provider.</param>
    Task<OrderResponse> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<List<OrderSummaryResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<OrderResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
