using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class UserProfileTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Db() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static async Task<JsonElement> Login(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..12]; var email = tag + "@example.test";
        await Token(client);
        var registered = await Json(await client.PostAsJsonAsync("/api/v1/auth/register", new
            { email, password = "Local-password-72", handle = "Profile_" + tag, displayName = "Reader" }, Ct), HttpStatusCode.Created);
        var signedIn = await Json(await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Local-password-72" }, Ct));
        Assert.Equal(registered.GetProperty("id").GetGuid(), signedIn.GetProperty("id").GetGuid());
        await Token(client); return signedIn;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
    private static async Task<JsonElement> Patch(HttpClient client, string payload) =>
        await Json(await client.PatchAsync("/api/v1/users/me", new StringContent(payload, Encoding.UTF8, "application/json"), Ct));

    [Fact]
    public async Task Public_id_and_handle_reads_expose_only_public_profile_fields()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var anonymous = Client(factory);
        var account = await Login(owner); var id = account.GetProperty("id").GetGuid(); var handle = account.GetProperty("handle").GetString()!;
        foreach (var route in new[] { $"/api/v1/users/{id}", $"/api/v1/users/by-handle/{handle.ToUpperInvariant()}", $"/API/V1/USERS/{id}/" })
        {
            var response = await anonymous.GetAsync(route, Ct); var profile = await Json(response);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(new[] { "bio", "createdAt", "displayName", "handle", "id", "profileTextHidden" }, profile.EnumerateObject().Select(x => x.Name).Order().ToArray());
            Assert.False(profile.GetProperty("profileTextHidden").GetBoolean());
            Assert.Equal(id, profile.GetProperty("id").GetGuid()); Assert.Equal(handle, profile.GetProperty("handle").GetString());
            Assert.Equal("Reader", profile.GetProperty("displayName").GetString()); Assert.Equal(JsonValueKind.Null, profile.GetProperty("bio").ValueKind);
            Assert.Equal(account.GetProperty("createdAt").GetDateTimeOffset(), profile.GetProperty("createdAt").GetDateTimeOffset());
        }
    }
    [Fact]
    public async Task Owner_partial_edits_preserve_omissions_clear_bio_and_keep_private_response()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory);
        var account = await Login(owner); var otherAccount = await Login(other); var id = account.GetProperty("id").GetGuid();
        var edited = await Patch(owner, "{\"displayName\":\"  New reader  \",\"bio\":\"  <b>Literal</b>\\nBio  \"}");
        Assert.Equal("New reader", edited.GetProperty("displayName").GetString()); Assert.Equal("<b>Literal</b>\nBio", edited.GetProperty("bio").GetString());
        Assert.Equal(account.GetProperty("email").GetString(), edited.GetProperty("email").GetString()); Assert.Equal(account.GetProperty("handle").GetString(), edited.GetProperty("handle").GetString());
        var onlyName = await Patch(owner, "{\"displayName\":\"Second\"}"); Assert.Equal("<b>Literal</b>\nBio", onlyName.GetProperty("bio").GetString());
        var onlyBio = await Patch(owner, "{\"bio\":\"Third\"}"); Assert.Equal("Second", onlyBio.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, (await Patch(owner, "{\"bio\":null}")).GetProperty("bio").ValueKind);
        await Patch(owner, "{\"bio\":\"Again\"}"); Assert.Equal(JsonValueKind.Null, (await Patch(owner, "{\"bio\":\" \\n \\t \"}")).GetProperty("bio").ValueKind);
        var own = await Json(await owner.GetAsync("/api/v1/users/me", Ct)); Assert.Equal("Second", own.GetProperty("displayName").GetString());
        var unchanged = await Json(await other.GetAsync("/api/v1/users/me", Ct)); Assert.Equal(otherAccount.GetRawText(), unchanged.GetRawText());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await other.PatchAsJsonAsync($"/api/v1/users/{id}", new { displayName = "Hijack" }, Ct)).StatusCode);
    }
    [Fact]
    public async Task Edit_requires_session_and_csrf_and_missing_targets_stay_private()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var anonymous = Client(factory); await Login(owner);
        var denied = await anonymous.PatchAsJsonAsync("/api/v1/users/me", new { displayName = "No session" }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); Assert.True(denied.Headers.CacheControl?.NoStore);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync("/api/v1/users/me", new { bio = "No token" }, Ct)).StatusCode);
        foreach (var route in new[] { $"/api/v1/users/{Guid.NewGuid()}", "/api/v1/users/by-handle/missing_handle", $"/api/v1/users/{Guid.Empty}" })
        { var response = await anonymous.GetAsync(route, Ct); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore); }
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/v1/users/by-handle/bad-handle", Ct)).StatusCode);
    }
    [Theory]
    [InlineData("{}")] [InlineData("""{"displayName":null}""")] [InlineData("""{"displayName":" "}""")]
    [InlineData("{\"displayName\":\"bad\\nname\"}")] [InlineData("{\"displayName\":\"\\uD800\"}")]
    [InlineData("{\"bio\":\"bad\\u0000\"}")] [InlineData("{\"bio\":\"\\uD800\"}")]
    [InlineData("{\"bio\":123}")] [InlineData("{\"displayName\":true}")]
    [InlineData("{\"handle\":\"rename\"}")] [InlineData("{\"email\":\"another@example.test\"}")]
    [InlineData("{\"bio\":null,\"bio\":\"masked\"}")] [InlineData("{\"role\":\"Admin\"}")]
    [InlineData("{\"hasBio\":true,\"bio\":\"injected\"}")]
    public async Task Invalid_edits_preserve_the_complete_private_profile(string payload)
    {
        await using var factory = Factory(); using var owner = Client(factory); await Login(owner);
        var before = await Json(await owner.GetAsync("/api/v1/users/me", Ct));
        var response = await owner.PatchAsync("/api/v1/users/me", new StringContent(payload, Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(before.GetRawText(), (await Json(await owner.GetAsync("/api/v1/users/me", Ct))).GetRawText());
    }
}
