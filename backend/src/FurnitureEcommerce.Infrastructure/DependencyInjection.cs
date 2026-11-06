using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Infrastructure.Persistence;
using FurnitureEcommerce.Infrastructure.Persistence.Repositories;
using FurnitureEcommerce.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string uploadsPath)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        var uploadsBaseUrl = configuration["FileStorage:BaseUrl"]
            ?? throw new InvalidOperationException("'FileStorage:BaseUrl' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddSingleton<IFileStorage>(provider => new LocalFileStorage(
            uploadsPath,
            uploadsBaseUrl,
            provider.GetRequiredService<ILogger<LocalFileStorage>>()));

        return services;
    }
}
