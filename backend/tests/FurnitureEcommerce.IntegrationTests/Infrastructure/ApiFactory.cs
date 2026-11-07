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
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-of-at-least-32-bytes");
        builder.UseSetting("Jwt:ExpiryMinutes", "60");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(contentRoot);
        await database.StartAsync();

        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();

        respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await respawner.ResetAsync(connection);
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
