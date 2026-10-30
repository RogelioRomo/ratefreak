namespace RateFreak.Application.Social;

public sealed record FollowUser(Guid Id, string Handle, string DisplayName, DateTimeOffset FollowedAt);
public sealed record FollowPage(IReadOnlyList<FollowUser> Items, int TotalCount, int Page, int PageSize);
public enum FollowStatus { Success, Missing, Self }

public interface IFollowService
{
    Task<FollowPage?> ListAsync(Guid userId, bool followers, int page, int pageSize, CancellationToken ct);
    Task<FollowStatus> SetAsync(Guid actorId, Guid targetId, bool following, CancellationToken ct);
}
