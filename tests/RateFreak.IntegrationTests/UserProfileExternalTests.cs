using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RateFreak.IntegrationTests;

public sealed partial class ExternalAccountTests
{
    [Theory]
    [InlineData("google")] [InlineData("github")]
    public async Task External_account_profile_edits_survive_a_fresh_provider_login(string provider)
    {
        var upstream = new Provider(); await using var factory = Enabled(upstream); using var client = Client(factory);
        using var completion = await Callback(client, provider, await Start(client, provider)); Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        await CompleteRegistration(client); await Token(client);
        using var edited = await client.PatchAsJsonAsync("/api/v1/users/me", new { bio = "External reader bio" }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode); Assert.Equal("External reader bio", (await edited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("bio").GetString());
        await Token(client); using var logout = await client.PostAsync("/api/v1/auth/logout", null, Cancellation); Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var login = await Callback(client, provider, await Start(client, provider)); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("External reader bio", (await login.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("bio").GetString());
        using var me = await client.GetAsync("/api/v1/users/me", Cancellation); Assert.Equal("External reader bio", (await me.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("bio").GetString());
    }
}
