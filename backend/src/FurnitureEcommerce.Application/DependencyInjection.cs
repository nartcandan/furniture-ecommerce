using FluentValidation;
using FurnitureEcommerce.Application.Categories;
using Microsoft.Extensions.DependencyInjection;

namespace FurnitureEcommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<CategoryService>();

        return services;
    }
}
