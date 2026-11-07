using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

/// <summary>Catalog reads for admins. Unlike the public endpoints, these include inactive records.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin")]
public class AdminCatalogController(
    ICategoryService categoryService,
    IProductService productService) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryResponse>>> GetCategories(CancellationToken cancellationToken)
    {
        return await categoryService.GetAllAsync(cancellationToken);
    }

    [HttpGet("products")]
    public async Task<ActionResult<PagedResult<ProductResponse>>> GetProducts(
        [FromQuery] ProductQuery query,
        CancellationToken cancellationToken)
    {
        return await productService.GetAllPagedAsync(query, cancellationToken);
    }

    [HttpGet("products/{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await productService.GetByIdAsync(id, cancellationToken);
    }
}
