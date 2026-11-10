using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Domain.Collections;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    private static string ProfileTarget(Guid id) => $"/api/v1/moderation/profiles/{id}";
    private static string ProfileReport(Guid id) => $"/api/v1/users/{id}/reports";
    private static async Task HideProfile(HttpClient staff, Guid id, bool hidden)
    {
        var target = await Json(await staff.GetAsync(ProfileTarget(id), Ct));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync(ProfileTarget(id) + "/visibility",
            new { hidden, reason = "Profile policy", expectedTargetUpdatedAt = target.GetProperty("updatedAt").GetString() }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Profile_reporting_uses_the_existing_account_budget_and_errors_are_no_store()
    {
        await using var f = Factory(); using var reporter = Client(f); await Login(reporter);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.NotFound, (await reporter.PostAsJsonAsync(ProfileReport(Guid.NewGuid()), new { reason = "spam" }, Ct)).StatusCode);
        var limited = await reporter.PostAsJsonAsync(ProfileReport(Guid.NewGuid()), new { reason = "spam" }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.True(limited.Headers.CacheControl?.NoStore); Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task Profile_reporting_requires_other_account_csrf_and_current_staff_for_inspection()
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); using var staff = Client(f); using var anon = Client(f);
        var id = await Login(owner); await Login(reporter); await Role(await Login(staff), "Moderator");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync(ProfileReport(id), new { reason = "spam" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(ProfileReport(id), new { reason = "spam" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(ProfileTarget(id), Ct)).StatusCode);
        reporter.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await reporter.PostAsJsonAsync(ProfileReport(id), new { reason = "spam" }, Ct)).StatusCode); await Token(reporter);
        var r = await reporter.PostAsJsonAsync(ProfileReport(id), new { reason = "other", details = "Profile text" }, Ct);
        Assert.True(r.Headers.CacheControl?.NoStore); var report = await Json(r, HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await reporter.PostAsJsonAsync(ProfileReport(id), new { reason = "spam" }, Ct)).StatusCode);
        var detail = await Json(await staff.GetAsync($"/api/v1/moderation/reports/{report.GetProperty("id").GetGuid()}", Ct));
        Assert.Equal("profile", detail.GetProperty("target").GetProperty("targetType").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await reporter.PostAsJsonAsync(ProfileReport(Guid.NewGuid()), new { reason = "spam" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Profile_hide_masks_public_identity_preserves_owner_text_and_requires_staff_restore_after_edits()
    {
        await using var f = Factory(); using var owner = Client(f); using var staff = Client(f); using var anon = Client(f);
        var id = await Login(owner); await Role(await Login(staff), "Moderator");
        await Json(await owner.PatchAsJsonAsync("/api/v1/users/me", new { displayName = "Original offensive name", bio = "Original bio" }, Ct));
        var account = await Json(await owner.GetAsync("/api/v1/users/me", Ct)); var handle = account.GetProperty("handle").GetString();
        var (_, review) = await Review(owner); var reviewId = review.GetProperty("id").GetGuid();
        var comment = await Json(await owner.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { body = "Public comment" }, Ct), HttpStatusCode.Created);
        var staffAccount = await Json(await staff.GetAsync("/api/v1/users/me", Ct)); var staffId = staffAccount.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsync($"/api/v1/users/{staffId}/follow", null, Ct)).StatusCode);
        Guid collectionId;
        await using (var db = Db()) { var collection = new Collection(id, "Public list", null, "public"); db.Add(collection); await db.SaveChangesAsync(Ct); collectionId = collection.Id; }
        await HideProfile(staff, id, true);
        var profile = await Json(await anon.GetAsync($"/api/v1/users/{id}", Ct));
        Assert.Equal(handle, profile.GetProperty("displayName").GetString()); Assert.Equal(JsonValueKind.Null, profile.GetProperty("bio").ValueKind); Assert.True(profile.GetProperty("profileTextHidden").GetBoolean());
        var byHandle = await Json(await anon.GetAsync($"/api/v1/users/by-handle/{handle}", Ct)); Assert.Equal(handle, byHandle.GetProperty("displayName").GetString());
        var publicReview = await Json(await anon.GetAsync($"/api/v1/reviews/{reviewId}", Ct)); Assert.Equal(handle, publicReview.GetProperty("author").GetProperty("displayName").GetString());
        var publicComment = await Json(await anon.GetAsync($"/api/v1/comments/{comment.GetProperty("id").GetGuid()}", Ct)); Assert.Equal(handle, publicComment.GetProperty("author").GetProperty("displayName").GetString());
        var followers = await Json(await anon.GetAsync($"/api/v1/users/{staffId}/followers", Ct)); Assert.Equal(handle, followers.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id).GetProperty("displayName").GetString());
        var publicCollection = await Json(await anon.GetAsync($"/api/v1/collections/{collectionId}", Ct)); Assert.Equal(handle, publicCollection.GetProperty("collection").GetProperty("owner").GetProperty("displayName").GetString());
        account = await Json(await owner.GetAsync("/api/v1/users/me", Ct)); Assert.Equal("Original offensive name", account.GetProperty("displayName").GetString()); Assert.True(account.GetProperty("profileTextHidden").GetBoolean());
        var before = await Json(await staff.GetAsync(ProfileTarget(id), Ct)); Assert.Equal("Original offensive name", before.GetProperty("body").GetString());
        await Json(await owner.PatchAsJsonAsync("/api/v1/users/me", new { displayName = "Corrected name", bio = "Corrected bio" }, Ct));
        var corrected = await Json(await staff.GetAsync(ProfileTarget(id), Ct)); Assert.True(corrected.GetProperty("hidden").GetBoolean()); Assert.NotEqual(before.GetProperty("updatedAt").GetString(), corrected.GetProperty("updatedAt").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(ProfileTarget(id) + "/visibility", new { hidden = false, reason = "Stale approval", expectedTargetUpdatedAt = before.GetProperty("updatedAt").GetString() }, Ct)).StatusCode);
        profile = await Json(await anon.GetAsync($"/api/v1/users/{id}", Ct)); Assert.Equal(handle, profile.GetProperty("displayName").GetString());
        await HideProfile(staff, id, false); profile = await Json(await anon.GetAsync($"/api/v1/users/{id}", Ct)); Assert.Equal("Corrected name", profile.GetProperty("displayName").GetString()); Assert.Equal("Corrected bio", profile.GetProperty("bio").GetString());
    }

    [Fact]
    public async Task Profile_versions_ignore_noop_and_security_changes_and_stale_identity_cannot_republish()
    {
        await using var f = Factory(); using var owner = Client(f); using var staff = Client(f);
        var id = await Login(owner); await Role(await Login(staff), "Moderator");
        var before = await Json(await staff.GetAsync(ProfileTarget(id), Ct));
        await Json(await owner.PatchAsJsonAsync("/api/v1/users/me", new { displayName = "Reader", bio = (string?)null }, Ct));
        var same = await Json(await staff.GetAsync(ProfileTarget(id), Ct)); Assert.Equal(before.GetProperty("updatedAt").GetString(), same.GetProperty("updatedAt").GetString());
        await using var scope = f.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>(); var stale = (await users.FindByIdAsync(id.ToString()))!;
        await HideProfile(staff, id, true); var hidden = await Json(await staff.GetAsync(ProfileTarget(id), Ct));
        var result = await users.UpdateSecurityStampAsync(stale); Assert.False(result.Succeeded);
        await using var fresh = f.Services.CreateAsyncScope(); var manager = fresh.ServiceProvider.GetRequiredService<UserManager<AccountUser>>(); Assert.True((await manager.UpdateSecurityStampAsync((await manager.FindByIdAsync(id.ToString()))!)).Succeeded);
        var after = await Json(await staff.GetAsync(ProfileTarget(id), Ct)); Assert.True(after.GetProperty("hidden").GetBoolean()); Assert.Equal(hidden.GetProperty("updatedAt").GetString(), after.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task Profile_reports_resolve_with_versions_audit_and_own_profile_actions_are_denied()
    {
        await using var f = Factory(); using var owner = Client(f); using var reporter = Client(f); using var staff = Client(f);
        var id = await Login(owner); await Role(id, "Moderator"); await Login(reporter); await Role(await Login(staff), "Moderator");
        var report = await Json(await reporter.PostAsJsonAsync(ProfileReport(id), new { reason = "harassment" }, Ct), HttpStatusCode.Created);
        var rid = report.GetProperty("id").GetGuid(); var path = $"/api/v1/moderation/reports/{rid}/resolve";
        var target = await Json(await staff.GetAsync(ProfileTarget(id), Ct)); var version = target.GetProperty("updatedAt").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(ProfileTarget(id) + "/visibility", new { hidden = true, reason = "Own", expectedTargetUpdatedAt = version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path, new { decision = "dismiss", reason = "Own", expectedTargetUpdatedAt = version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(path, new { decision = "hide", reason = "Stale", expectedTargetUpdatedAt = DateTimeOffset.UnixEpoch }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync(path, new { decision = "hide", reason = "Profile hidden", expectedTargetUpdatedAt = version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reporter.PostAsJsonAsync(ProfileReport(id), new { reason = "spam" }, Ct)).StatusCode);
        var audit = await Json(await staff.GetAsync($"/api/v1/moderation/audit?targetType=profile&targetId={id}", Ct)); Assert.Equal("Profile hidden", audit.GetProperty("items")[0].GetProperty("reason").GetString());
    }
}
