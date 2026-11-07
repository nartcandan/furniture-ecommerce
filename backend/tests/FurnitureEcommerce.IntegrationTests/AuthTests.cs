using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Email = "ayse@example.com";
    private const string Password = "Sifre1234";

    private static RegisterRequest NewRegistration(string email = Email, string password = Password) =>
        new("Ayse", "Yilmaz", email, password);

    private Task<HttpResponseMessage> RegisterAsync(RegisterRequest request) =>
        Client.PostAsJsonAsync("/api/auth/register", request);

    private Task<HttpResponseMessage> LoginAsync(string email = Email, string password = Password) =>
        Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    [Fact]
    public async Task Register_ReturnsUserAndToken()
    {
        var response = await RegisterAsync(NewRegistration());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(Email, auth!.User.Email);
        Assert.Equal("Ayse", auth.User.FirstName);
        Assert.NotEqual(Guid.Empty, auth.User.Id);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(auth.AccessToken);

        Assert.Equal(auth.User.Id.ToString(), token.Subject);
        Assert.Equal(Email, token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(ApiFactory.TestJwtIssuer, token.Issuer);
        Assert.Contains(ApiFactory.TestJwtIssuer, token.Audiences);
    }

    [Fact]
    public async Task Register_DoesNotReturnPasswordData()
    {
        var response = await RegisterAsync(NewRegistration());

        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ayse@example.com")]
    [InlineData("AYSE@example.com")]
    [InlineData(" ayse@example.com ")]
    public async Task Register_ReturnsConflict_ForRegisteredEmail(string email)
    {
        await RegisterAsync(NewRegistration());

        var response = await RegisterAsync(NewRegistration(email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_CreatesOneUser_ForConcurrentRequests()
    {
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => RegisterAsync(NewRegistration())));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(4, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public async Task Register_ReturnsBadRequest_ForWeakPassword(string password)
    {
        var response = await RegisterAsync(NewRegistration(password: password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_AcceptsEmailWithNonAsciiCharacters()
    {
        var response = await RegisterAsync(NewRegistration("ayşe@example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        await RegisterAsync(NewRegistration());

        var response = await LoginAsync(" AYSE@example.com ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(Email, auth!.User.Email);
        Assert.NotEmpty(auth.AccessToken);
    }

    [Fact]
    public async Task Login_ReturnsSameError_ForWrongPasswordAndUnknownEmail()
    {
        await RegisterAsync(NewRegistration());

        var wrongPassword = await LoginAsync(password: "Wrong1234");
        var unknownEmail = await LoginAsync("nobody@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var wrongPasswordError = await wrongPassword.Content.ReadFromJsonAsync<ProblemBody>();
        var unknownEmailError = await unknownEmail.Content.ReadFromJsonAsync<ProblemBody>();

        Assert.Equal(wrongPasswordError!.Detail, unknownEmailError!.Detail);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_ForEmptyFields()
    {
        var response = await LoginAsync(string.Empty, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_LocksAccount_AfterFiveFailedAttempts()
    {
        await RegisterAsync(NewRegistration());

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(password: "Wrong1234");
        }

        var response = await LoginAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ResetsFailedAttempts_AfterSuccess()
    {
        await RegisterAsync(NewRegistration());

        for (var round = 0; round < 2; round++)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                await LoginAsync(password: "Wrong1234");
            }

            var response = await LoginAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private record ProblemBody(string? Detail);
}
