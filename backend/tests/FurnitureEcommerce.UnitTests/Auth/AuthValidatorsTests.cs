using FurnitureEcommerce.Application.Auth;

namespace FurnitureEcommerce.UnitTests.Auth;

public class AuthValidatorsTests
{
    private static RegisterRequest ValidRegistration() => new(
        FirstName: "Ayse",
        LastName: "Yilmaz",
        Email: "ayse@example.com",
        Password: "Sifre1234");

    [Fact]
    public void RegisterValidator_AcceptsValidRequest()
    {
        var result = new RegisterRequestValidator().Validate(ValidRegistration());

        Assert.True(result.IsValid);
    }

    public static TheoryData<RegisterRequest, string> InvalidRegistrations => new()
    {
        { ValidRegistration() with { FirstName = "" }, "FirstName" },
        { ValidRegistration() with { FirstName = new string('a', 101) }, "FirstName" },
        { ValidRegistration() with { LastName = "" }, "LastName" },
        { ValidRegistration() with { LastName = new string('a', 101) }, "LastName" },
        { ValidRegistration() with { Email = "" }, "Email" },
        { ValidRegistration() with { Email = "not-an-email" }, "Email" },
        { ValidRegistration() with { Email = new string('a', 250) + "@a.com1" }, "Email" },
        { ValidRegistration() with { Password = "" }, "Password" },
        { ValidRegistration() with { Password = new string('a', 129) }, "Password" }
    };

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public void RegisterValidator_RejectsInvalidField(RegisterRequest request, string expectedField)
    {
        var result = new RegisterRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == expectedField);
    }

    [Theory]
    [InlineData("", "Sifre1234", "Email")]
    [InlineData("ayse@example.com", "", "Password")]
    public void LoginValidator_RejectsEmptyField(string email, string password, string expectedField)
    {
        var result = new LoginRequestValidator().Validate(new LoginRequest(email, password));

        Assert.Contains(result.Errors, error => error.PropertyName == expectedField);
    }
}
