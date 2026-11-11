using FluentValidation;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Carts;
using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Orders;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Application.Products;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FurnitureEcommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        // Lets tests move the clock, for rules such as "cancel only on the day of payment".
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductImageService, ProductImageService>();

        return services;
    }
}
