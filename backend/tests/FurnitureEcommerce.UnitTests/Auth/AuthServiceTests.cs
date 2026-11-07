using FluentValidation;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Common.Exceptions;
using NSubstitute;

namespace FurnitureEcommerce.UnitTests.Auth;

public class AuthServiceTests
{
    private static readonly AuthUser User = new(Guid.NewGuid(), "ayse@example.com", "Ayse", "Yilmaz", []);
    private static readonly AccessToken Token = new("token", DateTime.UtcNow.AddHours(1));

    private readonly IIdentityService identityService = Substitute.For<IIdentityService>();
    private readonly ITokenService tokenService = Substitute.For<ITokenService>();
    private readonly AuthService service;

    public AuthServiceTests()
    {
        tokenService.CreateToken(User).Returns(Token);

        service = new AuthService(
            identityService,
            tokenService,
            new RegisterRequestValidator(),
            new LoginRequestValidator());
    }

    [Fact]
    public async Task RegisterAsync_TrimsEmail_AndReturnsToken()
    {
        identityService.CreateUserAsync(Arg.Any<NewUser>()).Returns(User);

        var response = await service.RegisterAsync(
            new RegisterRequest("Ayse", "Yilmaz", " ayse@example.com ", "Sifre1234"),
            CancellationToken.None);

        await identityService.Received(1).CreateUserAsync(
            Arg.Is<NewUser>(user => user.Email == "ayse@example.com"));
        Assert.Equal("token", response.AccessToken);
        Assert.Equal(User.Id, response.User.Id);
    }

    [Fact]
    public async Task RegisterAsync_DoesNotCreateUser_ForInvalidRequest()
    {
        await Assert.ThrowsAsync<ValidationException>(() => service.RegisterAsync(
            new RegisterRequest("", "Yilmaz", "ayse@example.com", "Sifre1234"),
            CancellationToken.None));

        await identityService.DidNotReceive().CreateUserAsync(Arg.Any<NewUser>());
    }

    [Fact]
    public async Task LoginAsync_ReturnsToken_ForValidCredentials()
    {
        identityService.ValidateCredentialsAsync("ayse@example.com", "Sifre1234").Returns(User);

        var response = await service.LoginAsync(
            new LoginRequest(" ayse@example.com ", "Sifre1234"),
            CancellationToken.None);

        Assert.Equal("token", response.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_AndCreatesNoToken_ForInvalidCredentials()
    {
        identityService.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns((AuthUser?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(
            new LoginRequest("ayse@example.com", "Wrong1234"),
            CancellationToken.None));

        tokenService.DidNotReceive().CreateToken(Arg.Any<AuthUser>());
    }
}
