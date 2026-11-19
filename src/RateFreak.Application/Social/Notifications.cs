namespace RateFreak.Application.Social;

public sealed record NotificationActor(Guid Id, string Handle, string DisplayName, string? AvatarUrl = null);
public sealed record NotificationDetails(Guid Id, string Kind, NotificationActor Actor, Guid? CommentId,
    Guid? ReviewId, Guid? MediaItemId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationPage(IReadOnlyList<NotificationDetails> Items, int Page, int PageSize, int TotalCount, int UnreadCount);
public interface INotificationService
{
    Task<NotificationPage> ListAsync(Guid recipient, int page, int pageSize, CancellationToken ct);
    Task<int> UnreadCountAsync(Guid recipient, CancellationToken ct);
    Task<bool> SetReadAsync(Guid recipient, Guid id, bool read, CancellationToken ct);
    Task<bool> DeleteAsync(Guid recipient, Guid id, CancellationToken ct);
    Task ReadAllAsync(Guid recipient, CancellationToken ct);
    Task ClearAsync(Guid recipient, CancellationToken ct);
}
