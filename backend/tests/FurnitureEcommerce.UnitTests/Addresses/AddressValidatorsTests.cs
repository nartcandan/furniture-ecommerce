using FurnitureEcommerce.Application.Addresses;

namespace FurnitureEcommerce.UnitTests.Addresses;

public class AddressValidatorsTests
{
    private static AddressRequest ValidAddress() => new(
        Title: "Home",
        RecipientFirstName: "Ayse",
        RecipientLastName: "Yilmaz",
        PhoneNumber: "+905551112233",
        City: "Istanbul",
        District: "Kadikoy",
        AddressLine: "Moda Cd. No: 1",
        PostalCode: "34710");

    [Theory]
    [InlineData("+905551112233", "34710")]
    [InlineData("05551112233", null)]
    [InlineData("5551112233", "06100")]
    public void Validator_AcceptsValidRequest(string phoneNumber, string? postalCode)
    {
        var request = ValidAddress() with { PhoneNumber = phoneNumber, PostalCode = postalCode };

        var result = new AddressRequestValidator().Validate(request);

        Assert.True(result.IsValid);
    }

    public static TheoryData<AddressRequest, string> InvalidAddresses => new()
    {
        { ValidAddress() with { Title = "" }, "Title" },
        { ValidAddress() with { Title = new string('a', 51) }, "Title" },
        { ValidAddress() with { RecipientFirstName = "" }, "RecipientFirstName" },
        { ValidAddress() with { RecipientFirstName = new string('a', 101) }, "RecipientFirstName" },
        { ValidAddress() with { RecipientLastName = "" }, "RecipientLastName" },
        { ValidAddress() with { RecipientLastName = new string('a', 101) }, "RecipientLastName" },
        { ValidAddress() with { PhoneNumber = "" }, "PhoneNumber" },
        { ValidAddress() with { PhoneNumber = "555 111 22 33" }, "PhoneNumber" },
        { ValidAddress() with { PhoneNumber = "555111223" }, "PhoneNumber" },
        { ValidAddress() with { PhoneNumber = "+9055511122334455" }, "PhoneNumber" },
        { ValidAddress() with { PhoneNumber = "phone-number" }, "PhoneNumber" },
        { ValidAddress() with { City = "" }, "City" },
        { ValidAddress() with { City = new string('a', 51) }, "City" },
        { ValidAddress() with { District = "" }, "District" },
        { ValidAddress() with { District = new string('a', 51) }, "District" },
        { ValidAddress() with { AddressLine = "" }, "AddressLine" },
        { ValidAddress() with { AddressLine = new string('a', 501) }, "AddressLine" },
        { ValidAddress() with { PostalCode = "" }, "PostalCode" },
        { ValidAddress() with { PostalCode = "3471" }, "PostalCode" },
        { ValidAddress() with { PostalCode = "34A10" }, "PostalCode" }
    };

    [Theory]
    [MemberData(nameof(InvalidAddresses))]
    public void Validator_RejectsInvalidField(AddressRequest request, string expectedField)
    {
        var result = new AddressRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == expectedField);
    }

    [Fact]
    public void Validator_ReportsMissingFields_WithoutThrowing()
    {
        // Fields left out of the JSON body arrive as null.
        var request = new AddressRequest(null!, null!, null!, null!, null!, null!, null!, null);

        var result = new AddressRequestValidator().Validate(request);

        Assert.Equal(7, result.Errors.Select(error => error.PropertyName).Distinct().Count());
    }
}
