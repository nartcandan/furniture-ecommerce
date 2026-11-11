using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AuthorizationTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string CategoriesPath = "/api/categories";

    private static readonly string Id = Guid.NewGuid().ToString();

    public static TheoryData<string, string> WriteEndpoints => new()
    {
        { "POST", CategoriesPath },
        { "PUT", $"{CategoriesPath}/{Id}" },
        { "DELETE", $"{CategoriesPath}/{Id}" },
        { "POST", "/api/products" },
        { "PUT", $"/api/products/{Id}" },
        { "PATCH", $"/api/products/{Id}/stock" },
        { "DELETE", $"/api/products/{Id}" },
        { "POST", $"/api/products/{Id}/images" },
        { "DELETE", $"/api/products/{Id}/images/{Id}" },
        { "PUT", $"/api/products/{Id}/images/order" },
        { "GET", "/api/admin/orders" },
        { "GET", $"/api/admin/orders/{Id}" },
        { "POST", $"/api/admin/orders/{Id}/ship" },
        { "POST", $"/api/admin/orders/{Id}/deliver" }
    };

    private static HttpRequestMessage NewRequest(string method, string path, string? token = null)
    {
        // The upload endpoint only matches multipart requests.
        HttpContent content = method == "POST" && path.EndsWith("/images", StringComparison.Ordinal)
            ? new MultipartFormDataContent { { new ByteArrayContent(PngBytes), "file", "photo.png" } }
            : new StringContent("{}", Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null : content
        };

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<string> GetCustomerTokenAsync()
    {
        var response = await AnonymousClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("Ayse", "Yilmaz", "ayse@example.com", "Sifre1234"));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        return auth!.AccessToken;
    }

    private static string CreateToken(
        string signingKey = ApiFactory.TestJwtSigningKey,
        string issuer = ApiFactory.TestJwtIssuer,
        string audience = ApiFactory.TestJwtIssuer,
        DateTime? expires = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddHours(1);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim("role", Roles.Admin)
            ]),
            IssuedAt = expiresAt.AddHours(-1),
            NotBefore = expiresAt.AddHours(-1),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        });
    }

    [Theory]
    [MemberData(nameof(WriteEndpoints))]
    public async Task WriteEndpoint_ReturnsUnauthorized_WithoutToken(string method, string path)
    {
        var response = await AnonymousClient.SendAsync(NewRequest(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(WriteEndpoints))]
    public async Task WriteEndpoint_ReturnsForbidden_ForCustomer(string method, string path)
    {
        var token = await GetCustomerTokenAsync();

        var response = await AnonymousClient.SendAsync(NewRequest(method, path, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(WriteEndpoints))]
    public async Task WriteEndpoint_PassesAuthorization_ForAdmin(string method, string path)
    {
        var response = await Client.SendAsync(NewRequest(method, path));

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Theory]
    [InlineData(CategoriesPath)]
    [InlineData("/api/products")]
    public async Task ReadEndpoint_IsPublic(string path)
    {
        var response = await AnonymousClient.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HandMadeToken_PassesAuthorization_WhenEverythingIsValid()
    {
        // Proves that the rejections below come from the changed value, not from this helper.
        var response = await AnonymousClient.SendAsync(NewRequest("POST", CategoriesPath, CreateToken()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<string> InvalidTokens => new()
    {
        CreateToken(signingKey: "another-signing-key-of-at-least-32-bytes-long"),
        CreateToken(issuer: "someone-else"),
        CreateToken(audience: "someone-else"),
        CreateToken(expires: DateTime.UtcNow.AddMinutes(-1)),
        "not-a-token"
    };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task WriteEndpoint_ReturnsUnauthorized_ForInvalidToken(string token)
    {
        var response = await AnonymousClient.SendAsync(NewRequest("POST", CategoriesPath, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WriteEndpoint_ReturnsUnauthorized_ForTokenWithChangedPayload()
    {
        var customerToken = await GetCustomerTokenAsync();
        var parts = customerToken.Split('.');
        var adminPayload = Base64UrlEncoder.Encode(
            Base64UrlEncoder.Decode(parts[1]).Replace(Roles.Customer, Roles.Admin));

        var response = await AnonymousClient.SendAsync(
            NewRequest("POST", CategoriesPath, $"{parts[0]}.{adminPayload}.{parts[2]}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("customer", HttpStatusCode.Forbidden)]
    public async Task ErrorResponse_HasProblemDetailsBody(string? user, HttpStatusCode expectedStatus)
    {
        var token = user is null ? null : await GetCustomerTokenAsync();

        var response = await AnonymousClient.SendAsync(NewRequest("POST", CategoriesPath, token));

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
