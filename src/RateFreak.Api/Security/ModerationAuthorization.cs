using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RateFreak.Application.Moderation;

namespace RateFreak.Api.Security;

internal sealed record ModerationRequirement(bool AdminOnly) : IAuthorizationRequirement;
internal sealed class ModerationAuthorization(IModerationService service) : AuthorizationHandler<ModerationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ModerationRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && await service.HasAccessAsync(id, requirement.AdminOnly, CancellationToken.None)) context.Succeed(requirement);
    }
}
