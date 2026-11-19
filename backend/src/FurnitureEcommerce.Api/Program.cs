using FurnitureEcommerce.Api.BackgroundServices;
using FurnitureEcommerce.Api.ExceptionHandling;
using FurnitureEcommerce.Api.Logging;
using FurnitureEcommerce.Api.OpenApi;
using FurnitureEcommerce.Application;
using FurnitureEcommerce.Infrastructure;
using FurnitureEcommerce.Infrastructure.Identity;
using FurnitureEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// Logs what happens before the settings are read, such as a missing setting that stops the API.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // The real logger, built once the settings are read. It replaces the startup logger above.
    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteToOutputs(builder.Environment));

    // The browser tests run the API in this environment, with a database of their own and a test payment page.
    var isE2E = builder.Environment.IsEnvironment("E2E");

    // The E2E environment keeps its uploads apart, so test images never mix with the ones of development.
    var uploadsPath = Path.Combine(
        builder.Environment.ContentRootPath,
        builder.Configuration["FileStorage:Directory"] ?? Path.Combine("wwwroot", "uploads"));
    Directory.CreateDirectory(uploadsPath);

    // Add services to the container.

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, uploadsPath, allowTestPayments: isE2E);
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // The tests turn this off and run the reconciliation themselves, at the moment they choose.
    if (builder.Configuration.GetValue("Reconciliation:Enabled", true))
    {
        builder.Services.AddHostedService<OrderReconciliationWorker>();
    }

    // The frontend runs on another origin. It sends the token in a header, so cookies are not allowed.
    var frontendOrigin = new Uri(
        builder.Configuration["Frontend:BaseUrl"]
            ?? throw new InvalidOperationException("'Frontend:BaseUrl' is not configured."))
        .GetLeftPart(UriPartial.Authority);
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(frontendOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()));

    builder.Services.AddProblemDetails();
    builder.Services
        .AddControllers(options =>
            // Required fields are checked by FluentValidation, so a malformed body reports one error.
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
        // Parser messages contain internal type names and must not reach clients.
        .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false);
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecurityTransformer>();
        options.AddOperationTransformer<BearerSecurityTransformer>();
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        // The E2E database starts empty for every run, so it is created here. Elsewhere migrations are a deploy step.
        if (isE2E)
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync();
    }

    // Configure the HTTP request pipeline.
    // One line per request with its status and duration, instead of several lines from ASP.NET Core.
    // It comes first, so it logs the status the client gets: a validation error handled below is a 400 here,
    // not the exception on its way to the handler. Image requests are left out.
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, exception) =>
            exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : httpContext.Request.Path.StartsWithSegments("/uploads") ? LogEventLevel.Verbose
            : LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnostics, httpContext) =>
        {
            if (httpContext.User.FindFirst("sub")?.Value is { } userId)
            {
                diagnostics.Set("UserId", userId);
            }
        };
    });

    app.UseExceptionHandler();
    // Gives empty 401 and 403 responses the same ProblemDetails body as other errors.
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
            options.AddPreferredSecuritySchemes(BearerSecurityTransformer.SchemeName));
    }

    app.UseHttpsRedirection();

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads",
        OnPrepareResponse = context =>
            context.Context.Response.Headers.XContentTypeOptions = "nosniff"
    });

    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
// The EF Core tools build the app to read the model and then stop it this way. That is not a failure.
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "The API stopped because of an error.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
