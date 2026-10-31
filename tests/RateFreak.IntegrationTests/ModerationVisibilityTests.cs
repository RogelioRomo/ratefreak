using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Hidden_parent_and_child_filter_all_public_paths_and_author_edits_preserve_holds()
    {
        await using var f = Factory(); using var author = Client(f); using var commenter = Client(f); using var mod = Client(f); using var anon = Client(f);
        await Login(author); await Login(commenter); await Role(await Login(mod), "Moderator"); var (media, review) = await Review(author); var id = review.GetProperty("id").GetGuid();
        var comments = $"/api/v1/reviews/{id}/comments";
        var child = await Json(await commenter.PostAsJsonAsync(comments, new { body = "Child" }, Ct), HttpStatusCode.Created); var childId = child.GetProperty("id").GetGuid(); var childPath = $"/api/v1/comments/{childId}";
        async Task Hide(string type, Guid target, JsonElement current, bool hidden)
        { Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsJsonAsync($"/api/v1/moderation/{type}/{target}/visibility", new { hidden, reason = "Moderated", expectedTargetUpdatedAt = current.GetProperty("updatedAt").GetDateTimeOffset() }, Ct)).StatusCode); }
        await Hide("comments", childId, child, true); await Hide("reviews", id, review, true);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/reviews/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(childPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(comments, Ct)).StatusCode);
        Assert.Equal(0, (await Json(await anon.GetAsync($"/api/v1/media/{media}/reviews", Ct))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await commenter.PostAsJsonAsync(comments, new { body = "No new thread entry" }, Ct)).StatusCode);
        await Json(await author.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { body = "Edited hidden review" }, Ct));
        await Json(await commenter.PatchAsJsonAsync(childPath, new { body = "Edited hidden child" }, Ct));
        var current = await Json(await mod.GetAsync($"/api/v1/moderation/reviews/{id}", Ct)); Assert.True(current.GetProperty("hidden").GetBoolean());
        await Hide("reviews", id, current, false);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(childPath, Ct)).StatusCode);
        Assert.Equal(0, (await Json(await anon.GetAsync(comments, Ct))).GetProperty("totalCount").GetInt32());
        var childCurrent = await Json(await mod.GetAsync($"/api/v1/moderation/comments/{childId}", Ct)); Assert.True(childCurrent.GetProperty("hidden").GetBoolean()); await Hide("comments", childId, childCurrent, false);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync(childPath, Ct)).StatusCode);
        Assert.Equal(1, (await Json(await anon.GetAsync(comments, Ct))).GetProperty("totalCount").GetInt32());
    }
}
