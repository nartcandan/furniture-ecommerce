using FurnitureEcommerce.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace FurnitureEcommerce.Api.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/images")]
public class ProductImagesController(IProductImageService productImageService) : ControllerBase
{
    // Leaves room for the multipart overhead around the largest allowed file.
    private const long MaxRequestSizeInBytes = ProductImageService.MaxFileSizeInBytes + 64 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxRequestSizeInBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeInBytes)]
    public async Task<ActionResult<ProductImageResponse>> Add(
        Guid productId,
        IFormFile file,
        [FromForm] string? altText,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        var image = await productImageService.AddAsync(productId, content, altText, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, image);
    }

    [HttpDelete("{imageId:guid}")]
    public async Task<IActionResult> Delete(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        await productImageService.DeleteAsync(productId, imageId, cancellationToken);
        return NoContent();
    }

    [HttpPut("order")]
    public async Task<ActionResult<List<ProductImageResponse>>> Reorder(
        Guid productId,
        ReorderProductImagesRequest request,
        CancellationToken cancellationToken)
    {
        return await productImageService.ReorderAsync(productId, request, cancellationToken);
    }
}
