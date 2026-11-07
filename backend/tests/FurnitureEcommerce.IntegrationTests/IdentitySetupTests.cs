using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Infrastructure.Identity;
using FurnitureEcommerce.Infrastructure.Persistence;
using FurnitureEcommerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class IdentitySetupTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string ValidSigningKey = "a-signing-key-of-at-least-32-bytes-long";

    private async Task SetCustomerRoleNameAsync(string normalizedName)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Roles
            .Where(role => role.Name == Roles.Customer)
            .ExecuteUpdateAsync(setters => setters.SetProperty(role => role.NormalizedName, normalizedName));
    }

    [Fact]
    public async Task Register_CreatesNoUser_WhenRoleCannotBeAssigned()
    {
        await SetCustomerRoleNameAsync("MISSING");

        try
        {
            var response = await AnonymousClient.PostAsJsonAsync(
                "/api/auth/register",
                new RegisterRequest("Ayse", "Yilmaz", "ayse@example.com", "Sifre1234"));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            // Resets keep the Roles table, so later tests depend on this.
            await SetCustomerRoleNameAsync("CUSTOMER");
        }

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await dbContext.Users.AnyAsync(user => user.Email == "ayse@example.com"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("second-admin@example.com", null)]
    [InlineData(null, "AdminPass123")]
    public async Task AdminSeeder_CreatesNoAccount_WithoutSettings(string? email, string? password)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userCount = await userManager.Users.CountAsync();

        var seeder = new AdminSeeder(
            userManager,
            new AdminUserOptions { Email = email, Password = password },
            NullLogger<AdminSeeder>.Instance);

        await seeder.SeedAsync();

        Assert.Equal(userCount, await userManager.Users.CountAsync());
    }

    [Fact]
    public async Task AdminSeeder_Fails_ForPasswordThatBreaksTheRules()
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var seeder = new AdminSeeder(
            userManager,
            new AdminUserOptions { Email = "second-admin@example.com", Password = "weak" },
            NullLogger<AdminSeeder>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(seeder.SeedAsync);
    }

    [Fact]
    public void JwtOptions_AcceptsValidSettings()
    {
        var options = new JwtOptions
        {
            Issuer = "issuer",
            Audience = "audience",
            SigningKey = ValidSigningKey,
            ExpiryMinutes = 60
        };

        options.Validate();
    }

    [Theory]
    [InlineData("", "audience", ValidSigningKey, 60)]
    [InlineData("issuer", "", ValidSigningKey, 60)]
    [InlineData("issuer", "audience", "", 60)]
    [InlineData("issuer", "audience", "too-short", 60)]
    [InlineData("issuer", "audience", ValidSigningKey, 0)]
    public void JwtOptions_RejectsInvalidSettings(string issuer, string audience, string key, int expiryMinutes)
    {
        var options = new JwtOptions
        {
            Issuer = issuer,
            Audience = audience,
            SigningKey = key,
            ExpiryMinutes = expiryMinutes
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
