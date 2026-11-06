using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponse>>> GetPaged(
        [FromQuery] ProductQuery query,
        CancellationToken cancellationToken)
    {
        return await productService.GetPagedAsync(query, cancellationToken);
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductResponse>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        return await productService.GetBySlugAsync(slug, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBySlug), new { slug = product.Slug }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        return await productService.UpdateAsync(id, request, cancellationToken);
    }

    [HttpPatch("{id:guid}/stock")]
    public async Task<ActionResult<ProductResponse>> AdjustStock(
        Guid id,
        AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        return await productService.AdjustStockAsync(id, request, cancellationToken);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
