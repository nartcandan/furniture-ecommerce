using FurnitureEcommerce.Api.Extensions;
using FurnitureEcommerce.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

/// <summary>The signed-in user's own orders. The user id always comes from the token.</summary>
[Authorize]
[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OrderSummaryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return await orderService.GetAllAsync(User.GetUserId(), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await orderService.GetByIdAsync(User.GetUserId(), id, cancellationToken);
    }

    /// <summary>
    /// Creates an order from the cart and takes its items out of stock.
    /// The order waits for payment, and the cart is kept until the order is paid.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await orderService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}
