using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Identity;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    TokenService tokens,
    ITeacherService teachers) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<RegisteredUserDto>> Register(RegisterRequest request)
    {
        var role = new[] { AppRoles.Student, AppRoles.Teacher }.FirstOrDefault(r => string.Equals(r, request.Role.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw AppException.BadRequest("Public registration is available for Student and Teacher accounts only.");
        if (!string.IsNullOrWhiteSpace(request.Phone) && await users.Users.AnyAsync(x => x.PhoneNumber == request.Phone.Trim()))
            throw AppException.BadRequest("Phone number is already in use.");

        // One transaction so a teacher never ends up without a profile row.
        await using var tx = await db.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PhoneNumber = request.Phone?.Trim()
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw AppException.BadRequest(string.Join(" ", created.Errors.Select(e => e.Description)));

        var roleResult = await users.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            throw new InvalidOperationException("Could not assign role: " + string.Join(" ", roleResult.Errors.Select(e => e.Description)));

        if (role == AppRoles.Teacher)
            await teachers.CreateEmptyProfileAsync(user.Id);

        await tx.CommitAsync();

        return Created("/api/auth/login", new RegisteredUserDto(user.Id, user.Email!, user.DisplayName, role));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !await users.CheckPasswordAsync(user, request.Password))
            throw AppException.Unauthorized("Invalid email or password.");

        var role = (await users.GetRolesAsync(user)).FirstOrDefault() ?? "";
        var (token, expiresAt) = tokens.Create(user, role);

        return new LoginResponse(token, expiresAt, user.Id, user.DisplayName, role);
    }

    /// <summary>Handy for checking a token from Swagger: returns who the server thinks you are.</summary>
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public ActionResult<object> Me()
    {
        var id = User.FindFirst("sub")?.Value;
        var role = User.FindFirst("role")?.Value;
        var name = User.FindFirst("name")?.Value;
        return new { userId = id, displayName = name, role };
    }
}
