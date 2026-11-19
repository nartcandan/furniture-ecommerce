using System.Net;
using FurnitureEcommerce.Application.Payments;
using FurnitureEcommerce.Domain.Entities;
using FurnitureEcommerce.Infrastructure;
using FurnitureEcommerce.Infrastructure.Payments;
using FurnitureEcommerce.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FurnitureEcommerce.IntegrationTests;

/// <summary>The test payment provider that the browser tests use in place of iyzico.</summary>
public class TestPaymentGatewayTests
{
    private const string CallbackUrl = "http://localhost:5069/api/payments/callback";

    private readonly TestPaymentGateway gateway = new();

    private static Order NewOrder(decimal total) => new()
    {
        TotalAmount = total,
        RecipientFirstName = "Ayse",
        RecipientLastName = "Yilmaz",
        PhoneNumber = "05321234567",
        City = "Istanbul",
        District = "Kadikoy",
        AddressLine = "Moda"
    };

    private async Task<(Order Order, string Token)> StartAsync(decimal total = 2501m)
    {
        var order = NewOrder(total);
        var start = await gateway.StartCheckoutAsync(
            new CheckoutRequest(order, new PaymentBuyer(Guid.NewGuid(), "Ayse", "Yilmaz", "a@example.com", null), CallbackUrl),
            CancellationToken.None);

        Assert.True(start.IsSuccess);
        return (order, start.Token!);
    }

    [Fact]
    public async Task StartCheckout_OpensThePageOnTheApiThatReceivesTheCallback()
    {
        var order = NewOrder(100m);
        var start = await gateway.StartCheckoutAsync(
            new CheckoutRequest(order, new PaymentBuyer(Guid.NewGuid(), "A", "B", "a@example.com", null), CallbackUrl),
            CancellationToken.None);

        Assert.Equal($"http://localhost:5069/test-payments/{start.Token}", start.PaymentPageUrl);
    }

    [Fact]
    public async Task Result_IsNotCompleted_UntilThePageIsFinished()
    {
        var (_, token) = await StartAsync();

        var result = await gateway.GetCheckoutResultAsync(token, CancellationToken.None);

        Assert.Equal(CheckoutState.NotCompleted, result.State);
    }

    [Fact]
    public async Task Pay_GivesAPaymentForTheOrderAndItsAmount_AndReturnsTheCallback()
    {
        var (order, token) = await StartAsync(2501m);

        var callback = gateway.Complete(token, pay: true);
        var result = await gateway.GetCheckoutResultAsync(token, CancellationToken.None);

        Assert.Equal(CallbackUrl, callback);
        Assert.Equal(CheckoutState.Paid, result.State);
        Assert.Equal(2501m, result.PaidAmount);
        Assert.Equal(order.Id.ToString(), result.OrderReference);
        Assert.Matches("^[0-9]{8}$", result.PaymentId);
    }

    [Fact]
    public async Task Decline_FailsThePayment()
    {
        var (_, token) = await StartAsync();

        gateway.Complete(token, pay: false);
        var result = await gateway.GetCheckoutResultAsync(token, CancellationToken.None);

        Assert.Equal(CheckoutState.Failed, result.State);
        Assert.Null(result.PaymentId);
    }

    [Fact]
    public async Task APage_IsFinishedOnce()
    {
        var (_, token) = await StartAsync();
        gateway.Complete(token, pay: false);

        var second = gateway.Complete(token, pay: true);
        var result = await gateway.GetCheckoutResultAsync(token, CancellationToken.None);

        Assert.Null(second);
        Assert.Equal(CheckoutState.Failed, result.State);
    }

    [Fact]
    public async Task UnknownToken_HasNoPageAndFails()
    {
        var result = await gateway.GetCheckoutResultAsync("unknown", CancellationToken.None);

        Assert.Null(gateway.Find("unknown"));
        Assert.Null(gateway.Complete("unknown", pay: true));
        Assert.Equal(CheckoutState.Failed, result.State);
    }

    [Fact]
    public async Task CancelPayment_RefundsOnce_LikeIyzico()
    {
        var first = await gateway.CancelPaymentAsync("12345678", CancellationToken.None);
        var second = await gateway.CancelPaymentAsync("12345678", CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.True(second.WasAlreadyCancelled);
    }
}

/// <summary>The test payment provider must never be reachable outside the E2E environment.</summary>
public class TestPaymentProviderGuardTests
{
    private static IConfiguration Settings(string? provider) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            ["FileStorage:BaseUrl"] = "http://localhost/uploads",
            ["Jwt:Issuer"] = "tests",
            ["Jwt:Audience"] = "tests",
            ["Jwt:SigningKey"] = ApiFactory.TestJwtSigningKey,
            ["Jwt:ExpiryMinutes"] = "60",
            ["Payments:CallbackUrl"] = "http://localhost/api/payments/callback",
            ["Payments:Provider"] = provider,
            ["Frontend:BaseUrl"] = "http://localhost:5173",
            ["Iyzico:ApiKey"] = "key",
            ["Iyzico:SecretKey"] = "secret",
            ["Iyzico:BaseUrl"] = "https://iyzico.example.test"
        })
        .Build();

    private static IPaymentGateway GatewayFor(string? provider, bool allowTestPayments)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(Settings(provider), Path.GetTempPath(), allowTestPayments);
        return services.BuildServiceProvider().GetRequiredService<IPaymentGateway>();
    }

    [Fact]
    public void TestProvider_StopsTheApi_OutsideE2E()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GatewayFor("Test", allowTestPayments: false));

        Assert.Contains("only allowed in the E2E environment", error.Message);
    }

    [Fact]
    public void TestProvider_IsUsed_InE2E()
    {
        Assert.IsType<TestPaymentGateway>(GatewayFor("Test", allowTestPayments: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Iyzico")]
    public void Iyzico_IsTheDefault(string? provider)
    {
        Assert.IsType<IyzicoPaymentGateway>(GatewayFor(provider, allowTestPayments: true));
    }

    [Fact]
    public void UnknownProvider_StopsTheApi()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GatewayFor("Stripe", allowTestPayments: true));

        Assert.Contains("'Payments:Provider' must be", error.Message);
    }
}

/// <summary>With the real provider, the test payment page does not exist.</summary>
[Collection(nameof(ApiCollection))]
public class TestPaymentPageTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Page_IsNotFound_WithoutTheTestProvider()
    {
        var page = await AnonymousClient.GetAsync("/test-payments/any-token");
        var finish = await AnonymousClient.PostAsync(
            "/test-payments/any-token",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["outcome"] = "pay" }));

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, finish.StatusCode);
    }
}
