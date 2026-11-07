using System.Net;
using System.Net.Http.Json;
using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.IntegrationTests.Infrastructure;

namespace FurnitureEcommerce.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AddressesTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string AddressesPath = "/api/addresses";

    private static AddressRequest NewAddress(string title = "Home") => new(
        title, "Ayse", "Yilmaz", "+905551112233", "Istanbul", "Kadikoy", "Moda Cd. No: 1", "34710");

    private static async Task<AddressResponse> CreateAddressAsync(HttpClient client, string title = "Home")
    {
        var response = await client.PostAsJsonAsync(AddressesPath, NewAddress(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AddressResponse>())!;
    }

    [Fact]
    public async Task Create_ReturnsAddress_WithLocation()
    {
        var customer = await CreateCustomerClientAsync();

        var response = await customer.PostAsJsonAsync(AddressesPath, NewAddress());
        var address = await response.Content.ReadFromJsonAsync<AddressResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/addresses/{address!.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Home", address.Title);
        Assert.Equal("34710", address.PostalCode);
    }

    [Fact]
    public async Task Create_AcceptsMissingPostalCode()
    {
        var customer = await CreateCustomerClientAsync();

        var response = await customer.PostAsJsonAsync(AddressesPath, NewAddress() with { PostalCode = null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_ForInvalidField()
    {
        var customer = await CreateCustomerClientAsync();

        var response = await customer.PostAsJsonAsync(AddressesPath, NewAddress() with { PhoneNumber = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnAddresses_InCreationOrder()
    {
        var ayse = await CreateCustomerClientAsync("ayse@example.com");
        var mehmet = await CreateCustomerClientAsync("mehmet@example.com");
        await CreateAddressAsync(ayse, "Home");
        await CreateAddressAsync(mehmet, "Mehmet's home");
        await CreateAddressAsync(ayse, "Work");

        var addresses = await ayse.GetFromJsonAsync<List<AddressResponse>>(AddressesPath);

        Assert.Equal(["Home", "Work"], addresses!.Select(address => address.Title));
    }

    [Fact]
    public async Task GetById_ReturnsAddress()
    {
        var customer = await CreateCustomerClientAsync();
        var created = await CreateAddressAsync(customer);

        var address = await customer.GetFromJsonAsync<AddressResponse>($"{AddressesPath}/{created.Id}");

        Assert.Equal(created, address);
    }

    [Fact]
    public async Task Update_ChangesAllFields()
    {
        var customer = await CreateCustomerClientAsync();
        var created = await CreateAddressAsync(customer);
        var update = new AddressRequest(
            "Work", "Mehmet", "Demir", "05321234567", "Ankara", "Cankaya", "Ataturk Blv. No: 5", null);

        var response = await customer.PutAsJsonAsync($"{AddressesPath}/{created.Id}", update);
        var stored = await customer.GetFromJsonAsync<AddressResponse>($"{AddressesPath}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new AddressResponse(
                created.Id, "Work", "Mehmet", "Demir", "05321234567", "Ankara", "Cankaya", "Ataturk Blv. No: 5", null),
            stored);
    }

    [Fact]
    public async Task Delete_RemovesAddress()
    {
        var customer = await CreateCustomerClientAsync();
        var created = await CreateAddressAsync(customer);

        var delete = await customer.DeleteAsync($"{AddressesPath}/{created.Id}");
        var get = await customer.GetAsync($"{AddressesPath}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task OtherUsersAddress_ReturnsNotFound_AndStaysUnchanged()
    {
        var owner = await CreateCustomerClientAsync("ayse@example.com");
        var other = await CreateCustomerClientAsync("mehmet@example.com");
        var address = await CreateAddressAsync(owner);
        var path = $"{AddressesPath}/{address.Id}";

        var get = await other.GetAsync(path);
        var put = await other.PutAsJsonAsync(path, NewAddress("Stolen"));
        var delete = await other.DeleteAsync(path);
        var stored = await owner.GetFromJsonAsync<AddressResponse>(path);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(address, stored);
    }

    [Fact]
    public async Task MissingAddress_ReturnsNotFound()
    {
        var customer = await CreateCustomerClientAsync();
        var path = $"{AddressesPath}/{Guid.NewGuid()}";

        var get = await customer.GetAsync(path);
        var put = await customer.PutAsJsonAsync(path, NewAddress());
        var delete = await customer.DeleteAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Endpoints_ReturnUnauthorized_WithoutToken()
    {
        var path = $"{AddressesPath}/{Guid.NewGuid()}";

        var getAll = await AnonymousClient.GetAsync(AddressesPath);
        var get = await AnonymousClient.GetAsync(path);
        var post = await AnonymousClient.PostAsJsonAsync(AddressesPath, NewAddress());
        var put = await AnonymousClient.PutAsJsonAsync(path, NewAddress());
        var delete = await AnonymousClient.DeleteAsync(path);

        Assert.All(
            [getAll, get, post, put, delete],
            response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
    }
}
