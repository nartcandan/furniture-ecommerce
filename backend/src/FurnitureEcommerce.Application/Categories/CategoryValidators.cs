using FluentValidation;
using FurnitureEcommerce.Application.Common;

namespace FurnitureEcommerce.Application.Categories;

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100)
            .Must(name => SlugGenerator.Generate(name).Length > 0)
            .WithMessage("Name must contain at least one letter or digit.");

        RuleFor(request => request.Description).MaximumLength(500);
    }
}

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Description).MaximumLength(500);
    }
}
