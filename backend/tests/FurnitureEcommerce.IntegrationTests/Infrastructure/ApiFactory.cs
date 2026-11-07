using FurnitureEcommerce.Infrastructure.Identity;
using FurnitureEcommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace FurnitureEcommerce.IntegrationTests.Infrastructure;

/// <summary>Runs the API in memory against a temporary PostgreSQL container.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtIssuer = "furniture-tests";
    public const string TestJwtSigningKey = "integration-tests-signing-key-of-at-least-32-bytes";
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "AdminPass123";

    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private readonly string contentRoot = Path.Combine(Path.GetTempPath(), $"furniture-tests-{Guid.NewGuid():N}");
    private Respawner respawner = null!;

    public string UploadsPath => Path.Combine(contentRoot, "wwwroot", "uploads");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(contentRoot);
        builder.UseSetting("ConnectionStrings:DefaultConnection", database.GetConnectionString());
        builder.UseSetting("FileStorage:BaseUrl", "http://localhost/uploads");
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtIssuer);
        builder.UseSetting("Jwt:SigningKey", TestJwtSigningKey);
        builder.UseSetting("Jwt:ExpiryMinutes", "60");
        builder.UseSetting("AdminUser:Email", AdminEmail);
        builder.UseSetting("AdminUser:Password", AdminPassword);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(contentRoot);
        await database.StartAsync();

        // The API seeds the admin while it starts, so the tables must exist before that.
        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;

        await using (var dbContext = new AppDbContext(dbContextOptions))
        {
            await dbContext.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();

        // Roles come from a migration, so a reset must keep them.
        respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory"), new Table("Roles")]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await respawner.ResetAsync(connection);

        await SeedAdminAsync();
    }

    public async Task SeedAdminAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await database.DisposeAsync();
        Directory.Delete(contentRoot, recursive: true);
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public class ApiCollection : ICollectionFixture<ApiFactory>;
