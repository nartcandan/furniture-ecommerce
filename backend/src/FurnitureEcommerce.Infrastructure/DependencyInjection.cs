using System.Text;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Infrastructure.Identity;
using FurnitureEcommerce.Infrastructure.Persistence;
using FurnitureEcommerce.Infrastructure.Persistence.Repositories;
using FurnitureEcommerce.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

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
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // UserName holds the email, so it must accept every character an email can have.
                options.User.AllowedUserNameCharacters = string.Empty;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("'Jwt' settings are not configured.");
        jwtOptions.Validate();

        services.AddSingleton(jwtOptions);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keeps claim names as they are in the token, such as "sub" and "role".
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RoleClaimType = JwtTokenService.RoleClaimType,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    // The token ends exactly at its expiry time, without the default 5 extra minutes.
                    ClockSkew = TimeSpan.Zero
                };
            });
        services.AddAuthorization();

        services.AddSingleton(
            configuration.GetSection(AdminUserOptions.SectionName).Get<AdminUserOptions>() ?? new AdminUserOptions());
        services.AddScoped<AdminSeeder>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddSingleton<IFileStorage>(provider => new LocalFileStorage(
            uploadsPath,
            uploadsBaseUrl,
            provider.GetRequiredService<ILogger<LocalFileStorage>>()));

        return services;
    }
}
