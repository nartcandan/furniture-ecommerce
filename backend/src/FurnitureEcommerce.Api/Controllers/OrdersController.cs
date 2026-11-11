using FurnitureEcommerce.Api.Extensions;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Domain.Entities;
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
    /// Send the customer to <c>paymentPageUrl</c> from the response to pay.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await orderService.CreateAsync(
            User.GetUserId(),
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>
    /// Cancels a paid order on the day it was paid, refunds the payment and puts the items back in stock.
    /// Answers 202 with the order still in Cancelling when the payment provider could not be reached;
    /// the cancellation is then finished in the background.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.CancelAsync(User.GetUserId(), id, cancellationToken);

        return order.Status == nameof(OrderStatus.Cancelling) ? Accepted(order) : order;
    }
}
