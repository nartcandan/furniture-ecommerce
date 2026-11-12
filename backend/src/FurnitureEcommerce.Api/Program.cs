using FurnitureEcommerce.Api.BackgroundServices;
using FurnitureEcommerce.Api.ExceptionHandling;
using FurnitureEcommerce.Api.OpenApi;
using FurnitureEcommerce.Application;
using FurnitureEcommerce.Infrastructure;
using FurnitureEcommerce.Infrastructure.Identity;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);

// Add services to the container.

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, uploadsPath);
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
