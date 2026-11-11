using FurnitureEcommerce.Application.Carts;

namespace FurnitureEcommerce.UnitTests.Carts;

public class CartValidatorsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    public void AddValidator_AcceptsValidRequest(int quantity)
    {
        var result = new AddCartItemRequestValidator().Validate(new AddCartItemRequest(Guid.NewGuid(), quantity));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddValidator_RejectsNonPositiveQuantity(int quantity)
    {
        var result = new AddCartItemRequestValidator().Validate(new AddCartItemRequest(Guid.NewGuid(), quantity));

        Assert.Contains(result.Errors, error => error.PropertyName == "Quantity");
    }

    [Fact]
    public void AddValidator_ReportsMissingFields()
    {
        // Fields left out of the JSON body arrive as their default values.
        var result = new AddCartItemRequestValidator().Validate(new AddCartItemRequest(Guid.Empty, 0));

        Assert.Equal(
            ["ProductId", "Quantity"],
            result.Errors.Select(error => error.PropertyName).Distinct().Order());
    }

    [Fact]
    public void UpdateValidator_AcceptsPositiveQuantity()
    {
        var result = new UpdateCartItemRequestValidator().Validate(new UpdateCartItemRequest(3));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateValidator_RejectsNonPositiveQuantity(int quantity)
    {
        var result = new UpdateCartItemRequestValidator().Validate(new UpdateCartItemRequest(quantity));

        Assert.Contains(result.Errors, error => error.PropertyName == "Quantity");
    }
}
