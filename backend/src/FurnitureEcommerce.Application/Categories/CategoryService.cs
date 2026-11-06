using FluentValidation;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Categories;

public class CategoryService(
    ICategoryRepository repository,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator)
{
    public async Task<List<CategoryResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var categories = await repository.GetAllAsync(cancellationToken);
        return categories.Select(ToResponse).ToList();
    }

    public async Task<CategoryResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var category = await repository.GetBySlugAsync(slug, cancellationToken)
            ?? throw new NotFoundException($"Category '{slug}' was not found.");

        return ToResponse(category);
    }

    public async Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var category = new Category
        {
            Name = request.Name,
            Slug = await SlugGenerator.GenerateUniqueAsync(
                request.Name,
                repository.SlugExistsAsync,
                cancellationToken),
            Description = request.Description
        };

        await repository.AddAsync(category, cancellationToken);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var category = await GetExistingAsync(id, cancellationToken);

        // The slug stays the same so that existing links keep working.
        category.Name = request.Name;
        category.Description = request.Description;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(category, cancellationToken);
        return ToResponse(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await GetExistingAsync(id, cancellationToken);
        await repository.DeleteAsync(category, cancellationToken);
    }

    private async Task<Category> GetExistingAsync(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category '{id}' was not found.");
    }

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.Description,
        category.IsActive);
}
