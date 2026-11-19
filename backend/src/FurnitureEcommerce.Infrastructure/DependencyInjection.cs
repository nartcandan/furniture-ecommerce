using System.Text;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Infrastructure.Identity;
using FurnitureEcommerce.Infrastructure.Payments;
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
    /// <param name="allowTestPayments">
    /// True only in the E2E environment. Elsewhere asking for the test payment provider stops the API,
    /// so a wrong setting can never put a store online that takes orders without taking money.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string uploadsPath,
        bool allowTestPayments = false)
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
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        AddPaymentGateway(services, configuration, allowTestPayments);
        services.AddSingleton(new PaymentSettings(
            configuration["Payments:CallbackUrl"]
                ?? throw new InvalidOperationException("'Payments:CallbackUrl' is not configured."),
            configuration["Frontend:BaseUrl"]
                ?? throw new InvalidOperationException("'Frontend:BaseUrl' is not configured.")));

        var pendingPaymentTimeout = TimeSpan.FromMinutes(
            configuration.GetValue("Reconciliation:PendingPaymentTimeoutMinutes", 35));

        if (pendingPaymentTimeout <= ReconciliationSettings.PaymentPageLifetime)
        {
            // A shorter value would close orders whose payment page can still be paid.
            throw new InvalidOperationException(
                "'Reconciliation:PendingPaymentTimeoutMinutes' must be more than 30, the life of a payment page.");
        }

        services.AddSingleton(new ReconciliationSettings(
            pendingPaymentTimeout,
            TimeSpan.FromMinutes(configuration.GetValue("Reconciliation:CancellingTimeoutMinutes", 2))));

        services.AddSingleton<IFileStorage>(provider => new LocalFileStorage(
            uploadsPath,
            uploadsBaseUrl,
            provider.GetRequiredService<ILogger<LocalFileStorage>>()));

        return services;
    }

    public const string IyzicoProvider = "Iyzico";
    public const string TestProvider = "Test";

    private static void AddPaymentGateway(
        IServiceCollection services,
        IConfiguration configuration,
        bool allowTestPayments)
    {
        var provider = configuration["Payments:Provider"] ?? IyzicoProvider;

        if (provider == TestProvider)
        {
            if (!allowTestPayments)
            {
                throw new InvalidOperationException(
                    "The test payment provider is only allowed in the E2E environment. Set 'Payments:Provider' to 'Iyzico'.");
            }

            services.AddSingleton<TestPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(serviceProvider => serviceProvider.GetRequiredService<TestPaymentGateway>());
            return;
        }

        if (provider != IyzicoProvider)
        {
            throw new InvalidOperationException($"'Payments:Provider' must be '{IyzicoProvider}' or '{TestProvider}'.");
        }

        var iyzicoOptions = configuration.GetSection(IyzicoOptions.SectionName).Get<IyzicoOptions>()
            ?? throw new InvalidOperationException("'Iyzico' settings are not configured.");
        iyzicoOptions.Validate();

        services.AddSingleton(iyzicoOptions);
        services.AddSingleton<IPaymentGateway, IyzicoPaymentGateway>();
    }
}
