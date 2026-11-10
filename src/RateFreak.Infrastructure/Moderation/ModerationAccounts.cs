using System.Data;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Moderation;

namespace RateFreak.Infrastructure.Moderation;

internal sealed partial class ModerationService
{
    public async Task<ModerationPage<StaffAccount>> AccountsAsync(string? q, bool staffOnly, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var roles = from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
                    select new { membership.UserId, role.NormalizedName };
        var accounts = db.Users.AsNoTracking().Select(user => new { user.Id, Handle = user.UserName!, user.DisplayName,
            IsAdmin = roles.Any(r => r.UserId == user.Id && r.NormalizedName == "ADMIN"),
            IsModerator = roles.Any(r => r.UserId == user.Id && r.NormalizedName == "MODERATOR") });
        if (staffOnly) accounts = accounts.Where(a => a.IsAdmin || a.IsModerator);
        if (!string.IsNullOrWhiteSpace(q))
        {
            // Explicit escaping keeps user-supplied %, _ and backslashes literal.
            var pattern = "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            accounts = accounts.Where(a => EF.Functions.ILike(a.Handle, pattern, "\\") || EF.Functions.ILike(a.DisplayName, pattern, "\\"));
        }
        var count = await accounts.CountAsync(ct);
        var items = await accounts.OrderBy(a => a.Handle).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new StaffAccount(a.Id, a.Handle, a.DisplayName, a.IsAdmin, a.IsModerator)).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(items, page, pageSize, count);
    }
}
