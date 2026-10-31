using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.IntegrationTests;

public sealed partial class UserProfileTests
{
    private async Task<string> Security(Guid id)
    {
        await using var db = Db();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id, Ct);
        return JsonSerializer.Serialize(new
        {
            user.Id, user.UserName, user.NormalizedUserName, user.Email, user.NormalizedEmail,
            user.EmailConfirmed, user.PasswordHash, user.SecurityStamp, user.ConcurrencyStamp,
            user.PhoneNumber, user.PhoneNumberConfirmed, user.TwoFactorEnabled, user.LockoutEnd,
            user.LockoutEnabled, user.AccessFailedCount, user.CreatedAt,
            Roles = await db.UserRoles.Where(x => x.UserId == id).OrderBy(x => x.RoleId).ToListAsync(Ct),
            Logins = await db.UserLogins.Where(x => x.UserId == id).OrderBy(x => x.LoginProvider).ToListAsync(Ct)
        });
    }
    [Fact]
    public async Task Profile_edits_preserve_all_identity_security_fields_and_existing_sessions()
    {
        await using var factory = Factory(); using var owner = Client(factory);
        var account = await Login(owner); var id = account.GetProperty("id").GetGuid();
        await using (var db = Db())
        {
            var role = new IdentityRole<Guid>("profile-test-" + Guid.NewGuid().ToString("N")) { Id = Guid.NewGuid() };
            db.Roles.Add(role); await db.SaveChangesAsync(Ct);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = id, RoleId = role.Id });
            db.UserLogins.Add(new IdentityUserLogin<Guid> { UserId = id, LoginProvider = "Google", ProviderKey = Guid.NewGuid().ToString(), ProviderDisplayName = "Google" });
            await db.SaveChangesAsync(Ct);
        }
        var before = await Security(id);
        var response = await owner.PatchAsJsonAsync("/API/V1/USERS/ME/", new { displayName = "Updated", bio = "Public biography" }, Ct);
        await Json(response); Assert.True(response.Headers.CacheControl?.NoStore);
        // Existing stamp validation may renew cookies independently of profile editing.
        Assert.Equal(before, await Security(id));
        var me = await Json(await owner.GetAsync("/api/v1/users/me", Ct)); Assert.Equal("Updated", me.GetProperty("displayName").GetString()); Assert.Equal("Public biography", me.GetProperty("bio").GetString());
        // A regular password login must also project the persisted bio.
        using var nextSession = Client(factory); await Token(nextSession);
        var login = await Json(await nextSession.PostAsJsonAsync("/api/v1/auth/login", new { email = account.GetProperty("email").GetString(), password = "Local-password-72" }, Ct));
        Assert.Equal("Public biography", login.GetProperty("bio").GetString());
    }
    [Fact]
    public async Task Updated_name_flows_to_authored_reviews_comments_and_follow_lists()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory);
        var account = await Login(owner); var second = await Login(other); var id = account.GetProperty("id").GetGuid(); var secondId = second.GetProperty("id").GetGuid();
        Guid mediaId;
        await using (var db = Db()) { var media = new MediaItem("movie", "Profile names"); db.Add(media); await db.SaveChangesAsync(Ct); mediaId = media.Id; }
        var review = await Json(await owner.PostAsJsonAsync($"/api/v1/media/{mediaId}/reviews", new { body = "Review" }, Ct), HttpStatusCode.Created); var rid = review.GetProperty("id").GetGuid();
        var comment = await Json(await owner.PostAsJsonAsync($"/api/v1/reviews/{rid}/comments", new { body = "Comment" }, Ct), HttpStatusCode.Created); var cid = comment.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync($"/api/v1/users/{secondId}/follow", null, Ct)).StatusCode);
        await Patch(owner, "{\"displayName\":\"New author\"}");
        foreach (var route in new[] { $"/api/v1/reviews/{rid}", $"/api/v1/comments/{cid}" })
        { var item = await Json(await other.GetAsync(route, Ct)); Assert.Equal("New author", item.GetProperty("author").GetProperty("displayName").GetString()); }
        var follows = await Json(await other.GetAsync($"/api/v1/users/{secondId}/followers", Ct));
        var follower = Assert.Single(follows.GetProperty("items").EnumerateArray());
        Assert.Contains("New author", follower.GetRawText()); Assert.DoesNotContain(account.GetProperty("email").GetString()!, follower.GetRawText());
    }
    [Fact]
    public async Task Utf16_boundaries_trim_input_and_accept_valid_surrogate_pairs()
    {
        await using var factory = Factory(); using var owner = Client(factory); await Login(owner);
        var name = string.Concat(Enumerable.Repeat("😀", 40)); var bio = string.Concat(Enumerable.Repeat("😀", 250));
        var accepted = await Json(await owner.PatchAsJsonAsync("/api/v1/users/me", new { displayName = " " + name + " ", bio = " " + bio + " " }, Ct));
        Assert.Equal(name, accepted.GetProperty("displayName").GetString()); Assert.Equal(bio, accepted.GetProperty("bio").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync("/api/v1/users/me", new { displayName = name + "x" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync("/api/v1/users/me", new { bio = bio + "x" }, Ct)).StatusCode);
    }
    [Fact]
    public async Task Read_and_write_budgets_are_separate_and_writes_are_per_account()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory); using var anonymous = Client(factory);
        var account = await Login(owner); await Login(other); var id = account.GetProperty("id").GetGuid();
        for (var i = 0; i < 10; i++) await Json(await owner.PatchAsJsonAsync("/api/v1/users/me", new { bio = "Bio " + i }, Ct));
        var limited = await owner.PatchAsJsonAsync("/api/v1/users/me", new { bio = "Over" }, Ct); Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.NotNull(limited.Headers.RetryAfter); Assert.True(limited.Headers.CacheControl?.NoStore);
        await Json(await other.PatchAsJsonAsync("/api/v1/users/me", new { bio = "Separate" }, Ct));
        for (var i = 0; i < 60; i++) await Json(await anonymous.GetAsync($"/api/v1/users/{id}", Ct));
        var readLimited = await anonymous.GetAsync($"/api/v1/users/by-handle/{account.GetProperty("handle").GetString()}/", Ct); Assert.Equal(HttpStatusCode.TooManyRequests, readLimited.StatusCode); Assert.True(readLimited.Headers.CacheControl?.NoStore); Assert.NotNull(readLimited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/v1/media", Ct)).StatusCode);
        await Json(await owner.GetAsync("/api/v1/users/me", Ct));
    }
    [Fact]
    public async Task Development_openapi_documents_profile_routes_and_separate_public_contract()
    {
        await using var factory = new ApiFactory(environment: "Development", connectionString: database.ConnectionString); using var client = Client(factory);
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", Ct); var paths = document.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/users/{userId}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/users/by-handle/{handle}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/users/me").TryGetProperty("patch", out _));
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.False(schemas.GetProperty("PublicProfile").GetProperty("properties").TryGetProperty("email", out _));
        Assert.True(schemas.GetProperty("AccountProfile").GetProperty("properties").TryGetProperty("email", out _));
        Assert.Equal(new[] { "bio", "displayName" }, schemas.GetProperty("UpdateProfileRequest").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name).Order().ToArray());
    }
}
