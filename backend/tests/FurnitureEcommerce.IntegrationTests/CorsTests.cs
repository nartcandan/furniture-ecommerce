using System.Net;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class CorsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private static HttpRequestMessage NewPreflight(string origin, string method, string headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/cart/items");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return request;
    }

    [Fact]
    public async Task Get_FromTheFrontendOrigin_AllowsTheOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add("Origin", ApiFactory.FrontendBaseUrl);

        var response = await AnonymousClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApiFactory.FrontendBaseUrl, response.Headers.GetValues(AllowOriginHeader).Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Get_FromAnotherOrigin_DoesNotAllowTheOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add("Origin", "http://evil.example.test");

        var response = await AnonymousClient.SendAsync(request);

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task Preflight_FromTheFrontendOrigin_AllowsTheTokenHeaderWithoutAToken()
    {
        var response = await AnonymousClient.SendAsync(
            NewPreflight(ApiFactory.FrontendBaseUrl, "POST", "authorization,content-type"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(ApiFactory.FrontendBaseUrl, response.Headers.GetValues(AllowOriginHeader).Single());
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());

        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
        Assert.Contains("authorization", allowedHeaders);
        Assert.Contains("content-type", allowedHeaders);
    }

    [Fact]
    public async Task Preflight_FromAnotherOrigin_DoesNotAllowTheOrigin()
    {
        var response = await AnonymousClient.SendAsync(
            NewPreflight("http://evil.example.test", "POST", "authorization,content-type"));

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task ErrorResponse_FromTheFrontendOrigin_AllowsTheOrigin()
    {
        // Without the header the browser hides the body, and the frontend could not show the error.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Add("Origin", ApiFactory.FrontendBaseUrl);

        var response = await AnonymousClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ApiFactory.FrontendBaseUrl, response.Headers.GetValues(AllowOriginHeader).Single());
    }
}
