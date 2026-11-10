using System.Text.Json.Serialization;
using RateFreak.Domain.PersonalMedia;

namespace RateFreak.Application.Moderation;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportRequest(string? Reason, string? Details = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResolveRequest(string? Decision, string? Reason, DateTimeOffset? ExpectedTargetUpdatedAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VisibilityRequest(bool? Hidden, string? Reason, DateTimeOffset? ExpectedTargetUpdatedAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RoleRequest(string? Reason);
public sealed record StaffIdentity(Guid Id, string Handle, string DisplayName);
public sealed record StaffAccount(Guid Id, string Handle, string DisplayName, bool IsAdmin, bool IsModerator);
public sealed record ModerationTarget(Guid Id, string TargetType, StaffIdentity? Author, string Body, bool IsSpoiler, bool Hidden, DateTimeOffset UpdatedAt, string? Description = null, string? ImageReference = null);
public sealed record ReportAcknowledgement(Guid Id, string Status, DateTimeOffset CreatedAt);
public sealed record ReportSummary(Guid Id, string TargetType, Guid TargetId, StaffIdentity? Reporter, string Reason, string Status, DateTimeOffset CreatedAt);
public sealed record ReportDetail(ReportSummary Report, string? Details, ModerationTarget? Target, string? Decision, string? ResolutionReason, DateTimeOffset? ResolvedAt);
public sealed record ModerationAuditEntry(Guid Id, DateTimeOffset CreatedAt, Guid? ActorId, string ActorKind,
    string Action, string TargetType, Guid TargetId, Guid? ReportId, string Reason,
    bool? Before, bool? After, DateTimeOffset? ReviewedUpdatedAt);
public sealed record ModerationPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public enum ModerationStatus { Success, NotFound, Conflict, Forbidden, Invalid }
public sealed record ModerationResult(ModerationStatus Status, object? Value = null);
public interface IModerationService
{
    Task<bool> HasAccessAsync(Guid actor, bool adminOnly, CancellationToken ct);
    Task<ModerationPage<StaffAccount>> AccountsAsync(string? q, bool staffOnly, int page, int pageSize, CancellationToken ct);
    Task<ModerationResult> ReportAsync(Guid actor, string type, Guid id, ReportRequest request, CancellationToken ct);
    Task<ModerationPage<ReportSummary>> ReportsAsync(string status, int page, int pageSize, CancellationToken ct);
    Task<ReportDetail?> ReportAsync(Guid id, CancellationToken ct);
    Task<ModerationTarget?> TargetAsync(string type, Guid id, CancellationToken ct);
    Task<ModerationPage<ModerationAuditEntry>> AuditAsync(string? type, Guid? id, int page, int pageSize, CancellationToken ct);
    Task<ModerationResult> ResolveAsync(Guid actor, Guid id, ResolveRequest request, CancellationToken ct);
    Task<ModerationResult> VisibilityAsync(Guid actor, string type, Guid id, VisibilityRequest request, CancellationToken ct);
    Task<ModerationResult> ModeratorAsync(Guid actor, Guid user, bool grant, string reason, CancellationToken ct);
    Task<ModerationResult> AppointAdminAsync(Guid user, string reason, CancellationToken ct);
    Task<ModerationResult> RevokeAdminAsync(Guid user, string reason, CancellationToken ct);
    Task<ModerationResult> RevokeAdminAsync(Guid actor, Guid user, string reason, CancellationToken ct);
}
public static class ModerationRules
{
    public static bool Text(string? value, bool required = true) => string.IsNullOrEmpty(value?.Trim()) ? !required : value.Trim().Length <= 1000 && PersonalMediaRules.ValidBody(value.Trim());
    public static bool Report(ReportRequest r) => r.Reason is "spam" or "harassment" or "hate" or "sexual_content" or "violence" or "spoiler" or "other" && Text(r.Details, r.Reason == "other");
}
