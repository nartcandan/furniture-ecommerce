using FluentValidation;
using FurnitureEcommerce.Application.Common;

namespace FurnitureEcommerce.Application.Products;

public abstract class ProductRequestValidator<T> : AbstractValidator<T>
    where T : IProductRequest
{
    protected ProductRequestValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200)
            .Must(name => SlugGenerator.Generate(name).Length > 0)
            .WithMessage("Name must contain at least one letter or digit.");

        RuleFor(request => request.Description).MaximumLength(5000);
        RuleFor(request => request.Price).GreaterThanOrEqualTo(0).PrecisionScale(10, 2, false);
        RuleFor(request => request.WidthCm).GreaterThan(0).PrecisionScale(6, 1, false);
        RuleFor(request => request.DepthCm).GreaterThan(0).PrecisionScale(6, 1, false);
        RuleFor(request => request.HeightCm).GreaterThan(0).PrecisionScale(6, 1, false);
        RuleFor(request => request.WeightKg).GreaterThan(0).PrecisionScale(6, 2, false);
        RuleFor(request => request.Material).MaximumLength(100);
        RuleFor(request => request.Color).MaximumLength(50);
    }
}

public class CreateProductRequestValidator : ProductRequestValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.StockQuantity).InclusiveBetween(0, 1_000_000);
    }
}

public class UpdateProductRequestValidator : ProductRequestValidator<UpdateProductRequest>;

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(request => request.Change)
            .NotEqual(0)
            .InclusiveBetween(-1_000_000, 1_000_000);
    }
}

public class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0);

        RuleFor(query => query.MaxPrice)
            .GreaterThanOrEqualTo(query => query.MinPrice)
            .When(query => query.MinPrice.HasValue && query.MaxPrice.HasValue);

        RuleFor(query => query.Sort)
            .Must(sort => sort is null || ProductSort.All.Contains(sort))
            .WithMessage($"Sort must be one of: {string.Join(", ", ProductSort.All)}.");
    }
}
