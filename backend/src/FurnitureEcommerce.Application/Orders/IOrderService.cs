namespace FurnitureEcommerce.Application.Orders;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken cancellationToken);
    Task<List<OrderSummaryResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<OrderResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
