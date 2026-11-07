namespace FurnitureEcommerce.Application.Categories;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetActiveAsync(CancellationToken cancellationToken);
    Task<List<CategoryResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<CategoryResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
