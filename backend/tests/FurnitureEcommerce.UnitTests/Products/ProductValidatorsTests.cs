using FurnitureEcommerce.Application.Products;

namespace FurnitureEcommerce.UnitTests.Products;

public class ProductValidatorsTests
{
    private static CreateProductRequest ValidRequest() => new(
        CategoryId: Guid.NewGuid(),
        Name: "Oslo Sofa",
        Description: null,
        Price: 3000.50m,
        StockQuantity: 5,
        WidthCm: 200.5m,
        DepthCm: null,
        HeightCm: null,
        WeightKg: 45.25m,
        Material: null,
        Color: null);

    [Fact]
    public void CreateValidator_AcceptsValidRequest()
    {
        var result = new CreateProductRequestValidator().Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    public static TheoryData<CreateProductRequest, string> InvalidRequests => new()
    {
        { ValidRequest() with { CategoryId = Guid.Empty }, "CategoryId" },
        { ValidRequest() with { Name = "" }, "Name" },
        { ValidRequest() with { Name = "!!!" }, "Name" },
        { ValidRequest() with { Name = new string('a', 201) }, "Name" },
        { ValidRequest() with { Price = -1 }, "Price" },
        { ValidRequest() with { Price = 1.123m }, "Price" },
        { ValidRequest() with { Price = 100_000_000m }, "Price" },
        { ValidRequest() with { StockQuantity = -1 }, "StockQuantity" },
        { ValidRequest() with { WidthCm = 0 }, "WidthCm" },
        { ValidRequest() with { WidthCm = 10.55m }, "WidthCm" },
        { ValidRequest() with { WeightKg = 1.234m }, "WeightKg" }
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void CreateValidator_RejectsInvalidField(CreateProductRequest request, string property)
    {
        var result = new CreateProductRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == property);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1_000_001, false)]
    public void StockValidator_ChecksChange(int change, bool isValid)
    {
        var result = new AdjustStockRequestValidator().Validate(new AdjustStockRequest(change));

        Assert.Equal(isValid, result.IsValid);
    }

    public static TheoryData<ProductQuery, bool> Queries => new()
    {
        { new ProductQuery(), true },
        { new ProductQuery { Sort = ProductSort.PriceAscending, MinPrice = 1, MaxPrice = 1 }, true },
        { new ProductQuery { Page = 0 }, false },
        { new ProductQuery { PageSize = 0 }, false },
        { new ProductQuery { PageSize = 101 }, false },
        { new ProductQuery { MinPrice = -1 }, false },
        { new ProductQuery { MinPrice = 5, MaxPrice = 1 }, false },
        { new ProductQuery { Sort = "unknown" }, false }
    };

    [Theory]
    [MemberData(nameof(Queries))]
    public void QueryValidator_ChecksQuery(ProductQuery query, bool isValid)
    {
        var result = new ProductQueryValidator().Validate(query);

        Assert.Equal(isValid, result.IsValid);
    }
}
