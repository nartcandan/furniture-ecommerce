using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Categories;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken);
    Task<List<Category>> GetActiveAsync(CancellationToken cancellationToken);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Category?> GetActiveBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ConflictException">The slug is already in use.</exception>
    Task AddAsync(Category category, CancellationToken cancellationToken);

    Task UpdateAsync(Category category, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.ConflictException">The category still has products.</exception>
    Task DeleteAsync(Category category, CancellationToken cancellationToken);
}
