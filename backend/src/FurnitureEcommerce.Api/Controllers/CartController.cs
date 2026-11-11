using FurnitureEcommerce.Api.Extensions;
using FurnitureEcommerce.Application.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

/// <summary>The signed-in user's own cart. The user id always comes from the token.</summary>
[Authorize]
[ApiController]
[Route("api/cart")]
public class CartController(ICartService cartService) : ControllerBase
{
    /// <summary>Returns the cart with current prices and stock. An unused cart is returned as empty.</summary>
    [HttpGet]
    public async Task<ActionResult<CartResponse>> Get(CancellationToken cancellationToken)
    {
        return await cartService.GetAsync(User.GetUserId(), cancellationToken);
    }

    /// <summary>Adds a product. If it is already in the cart, the quantity is increased.</summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> AddItem(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        return await cartService.AddItemAsync(User.GetUserId(), request, cancellationToken);
    }

    /// <summary>Sets the quantity of a product that is already in the cart.</summary>
    [HttpPut("items/{productId:guid}")]
    public async Task<ActionResult<CartResponse>> SetItemQuantity(
        Guid productId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        return await cartService.SetItemQuantityAsync(User.GetUserId(), productId, request, cancellationToken);
    }

    [HttpDelete("items/{productId:guid}")]
    public async Task<ActionResult<CartResponse>> RemoveItem(Guid productId, CancellationToken cancellationToken)
    {
        return await cartService.RemoveItemAsync(User.GetUserId(), productId, cancellationToken);
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await cartService.ClearAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
