using System.Net;

namespace RateFreak.IntegrationTests;

public sealed partial class UserProfileTests
{
    [Fact]
    public async Task Profile_method_and_route_errors_are_not_cacheable()
    {
        await using var factory = Factory(); using var owner = Client(factory); var account = await Login(owner);
        var id = account.GetProperty("id").GetGuid();
        var method = await owner.PutAsync($"/API/V1/USERS/{id}/", null, Ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, method.StatusCode); Assert.True(method.Headers.CacheControl?.NoStore);
        var route = await owner.GetAsync("/api/v1/users/by-handle/", Ct);
        Assert.Equal(HttpStatusCode.NotFound, route.StatusCode); Assert.True(route.Headers.CacheControl?.NoStore);
    }
}
