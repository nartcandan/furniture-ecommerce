using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

/// <summary>Order management for admins. Shows the orders of all customers.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/orders")]
public class AdminOrdersController(IAdminOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminOrderSummaryResponse>>> GetAll(
        [FromQuery] AdminOrderQuery query,
        CancellationToken cancellationToken)
    {
        return await orderService.GetPagedAsync(query, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await orderService.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>Marks a paid order as shipped. After this the customer can no longer cancel it.</summary>
    [HttpPost("{id:guid}/ship")]
    public async Task<ActionResult<AdminOrderResponse>> Ship(Guid id, CancellationToken cancellationToken)
    {
        return await orderService.ShipAsync(id, cancellationToken);
    }

    /// <summary>Marks a shipped order as delivered.</summary>
    [HttpPost("{id:guid}/deliver")]
    public async Task<ActionResult<AdminOrderResponse>> Deliver(Guid id, CancellationToken cancellationToken)
    {
        return await orderService.DeliverAsync(id, cancellationToken);
    }
}
