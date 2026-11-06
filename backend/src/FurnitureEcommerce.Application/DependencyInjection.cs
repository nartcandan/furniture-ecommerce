using FluentValidation;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Products;
using Microsoft.Extensions.DependencyInjection;

namespace FurnitureEcommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductImageService, ProductImageService>();

        return services;
    }
}
