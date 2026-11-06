namespace FurnitureEcommerce.Application.Categories;

public record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive);

public record CreateCategoryRequest(string Name, string? Description);

public record UpdateCategoryRequest(string Name, string? Description, bool IsActive);
