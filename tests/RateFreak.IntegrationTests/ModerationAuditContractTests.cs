using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Moderation;
using RateFreak.Domain.Moderation;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Audit_returns_response_contract_without_exposing_persistent_entities()
    {
        await using var factory = Factory();
        using var author = Client(factory);
        using var moderator = Client(factory);
        await Login(author);
        var moderatorId = await Login(moderator);
        await Role(moderatorId, "Moderator");
        var (_, review) = await Review(author);
        var reviewId = review.GetProperty("id").GetGuid();
        var version = review.GetProperty("updatedAt").GetDateTimeOffset();
        Assert.Equal(HttpStatusCode.NoContent, (await moderator.PostAsJsonAsync(Visibility(reviewId),
            new { hidden = true, reason = "Contract verification", expectedTargetUpdatedAt = version }, Ct)).StatusCode);

        var response = await moderator.GetAsync($"/api/v1/moderation/audit?targetType=review&targetId={reviewId}", Ct);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await Json(response);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(new[] { "action", "actorId", "actorKind", "after", "before", "createdAt", "id", "reason", "reportId", "reviewedUpdatedAt", "targetId", "targetType" },
            item.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(moderatorId, item.GetProperty("actorId").GetGuid());
        Assert.Equal("hide", item.GetProperty("action").GetString());
        Assert.Equal("account", item.GetProperty("actorKind").GetString());
        Assert.Equal("review", item.GetProperty("targetType").GetString());
        Assert.Equal(reviewId, item.GetProperty("targetId").GetGuid());
        Assert.Equal("Contract verification", item.GetProperty("reason").GetString());
        Assert.False(item.GetProperty("before").GetBoolean());
        Assert.True(item.GetProperty("after").GetBoolean());
        Assert.Equal(version, item.GetProperty("reviewedUpdatedAt").GetDateTimeOffset());

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IModerationService>();
        var page = await service.AuditAsync("review", reviewId, 1, 20, Ct);
        Assert.IsNotType<ModerationEvent>(Assert.Single(page.Items));
    }
}
