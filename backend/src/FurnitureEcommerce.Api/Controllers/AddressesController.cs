using FurnitureEcommerce.Api.Extensions;
using FurnitureEcommerce.Application.Addresses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

/// <summary>The signed-in user's own addresses. The user id always comes from the token.</summary>
[Authorize]
[ApiController]
[Route("api/addresses")]
public class AddressesController(IAddressService addressService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AddressResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return await addressService.GetAllAsync(User.GetUserId(), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AddressResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await addressService.GetByIdAsync(User.GetUserId(), id, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<AddressResponse>> Create(
        AddressRequest request,
        CancellationToken cancellationToken)
    {
        var address = await addressService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = address.Id }, address);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AddressResponse>> Update(
        Guid id,
        AddressRequest request,
        CancellationToken cancellationToken)
    {
        return await addressService.UpdateAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await addressService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
