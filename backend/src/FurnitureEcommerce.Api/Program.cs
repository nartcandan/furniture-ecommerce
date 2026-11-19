using FurnitureEcommerce.Api.BackgroundServices;
using FurnitureEcommerce.Api.ExceptionHandling;
using FurnitureEcommerce.Api.OpenApi;
using FurnitureEcommerce.Application;
using FurnitureEcommerce.Infrastructure;
using FurnitureEcommerce.Infrastructure.Identity;
using FurnitureEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
