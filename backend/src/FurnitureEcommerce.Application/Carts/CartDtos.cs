namespace FurnitureEcommerce.Application.Carts;

public record AddCartItemRequest(Guid ProductId, int Quantity);

public record UpdateCartItemRequest(int Quantity);

/// <param name="TotalAmount">Sum of all lines at current prices, including lines that have a warning.</param>
/// <param name="CanCheckout">True when the cart has items and no line has a warning.</param>
public record CartResponse(
    List<CartItemResponse> Items,
    int TotalQuantity,
    decimal TotalAmount,
    bool CanCheckout);

/// <param name="UnitPrice">The product's current price. The cart does not store prices.</param>
/// <param name="IsAvailable">False when the product or its category was deactivated after it was added.</param>
/// <param name="HasInsufficientStock">True when the stock fell below the quantity in the cart.</param>
public record CartItemResponse(
    Guid ProductId,
    string Name,
    string Slug,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int StockQuantity,
    bool IsAvailable,
    bool HasInsufficientStock);
