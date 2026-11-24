using System.Security.Claims;
using LearnSpace.Api.Common.Errors;

namespace LearnSpace.Api.Common;

/// <summary>Reads the user id and role from the JWT claims ("sub" and "role").</summary>
public static class CurrentUser
{
    public static Guid? TryGetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid GetUserId(this ClaimsPrincipal user)
        => user.TryGetUserId() ?? throw AppException.Unauthorized();

    public static string? GetRole(this ClaimsPrincipal user) => user.FindFirst("role")?.Value;
}
