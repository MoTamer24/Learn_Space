using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LearnSpace.Api.Modules.Groups;

namespace LearnSpace.Api.Modules.Identity;

[ApiController, Route("api/identity"), Authorize]
public class IdentityController(AppDbContext db, IdentityService service, UserManager<ApplicationUser> users, ITeacherService teachers, IGroupDirectory groups) : ControllerBase
{
    [HttpGet("users"), Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await db.Users.AsNoTracking().Select(x => new { x.Id, x.DisplayName, x.Email, x.PhoneNumber, x.IsActive }).ToListAsync(ct));

    [HttpPost("users"), Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CreateUser(CreateIdentityUserRequest request, CancellationToken ct)
    {
        try { var user = await service.CreateUserAsync(request, ct); return Created($"/api/identity/users/{user.Id}", new { user.Id, user.DisplayName, user.Email, Role = request.Role }); }
        catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); }
    }

    [HttpGet("students")]
    public async Task<IActionResult> Students(CancellationToken ct)
    {
        IReadOnlyCollection<Guid>? ids = null;
        var userId = CurrentUserId();
        if (User.IsInRole(AppRoles.Teacher)) ids = await groups.GetActiveStudentIdsForTeacherAsync(userId, ct);
        else if (User.IsInRole(AppRoles.Student)) { var id = await service.GetStudentIdForUserAsync(userId, ct); ids = id.HasValue ? [id.Value] : []; }
        else if (User.IsInRole(AppRoles.Parent)) ids = await service.GetStudentIdsForParentUserAsync(userId, ct);
        else if (!User.IsInRole(AppRoles.Admin)) ids = [];
        return Ok(await service.GetStudentsAsync(ids, ct));
    }

    [HttpPost("students"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)]
    public async Task<IActionResult> CreateStudent(CreateStudentRequest request, CancellationToken ct)
    {
        try { return CreatedAtAction(nameof(Students), await service.CreateStudentAsync(request, ct)); }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); }
    }

    [HttpDelete("students/{id:guid}"), Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteStudent(Guid id, CancellationToken ct)
    {
        var student = await db.Students.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (student is null) return NotFound();
        student.IsDeleted = true; student.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPost("parents"), Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CreateParent(CreateParentRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || !await users.IsInRoleAsync(user, AppRoles.Parent)) return BadRequest(new { error = "A user with the Parent role is required." });
        if (await db.Parents.AnyAsync(x => x.UserId == request.UserId, ct)) return Conflict(new { error = "Parent profile already exists." });
        var parent = new Parent { UserId = request.UserId };
        db.Parents.Add(parent); await db.SaveChangesAsync(ct); return Created($"/api/identity/parents/{parent.Id}", parent);
    }

    [HttpPost("student-parents"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)]
    public async Task<IActionResult> LinkParent(LinkParentRequest request, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(x => x.Id == request.StudentId && !x.IsDeleted, ct) || !await db.Parents.AnyAsync(x => x.Id == request.ParentId, ct)) return NotFound();
        if (await db.StudentParents.AnyAsync(x => x.StudentId == request.StudentId && x.ParentId == request.ParentId, ct)) return Conflict();
        var link = new StudentParent { StudentId = request.StudentId, ParentId = request.ParentId, Relation = request.Relation, IsPrimary = request.IsPrimary };
        db.StudentParents.Add(link); await db.SaveChangesAsync(ct); return Ok(link);
    }

    [HttpPost("staff"), Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CreateStaff(CreateStaffRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || !await users.IsInRoleAsync(user, AppRoles.Staff)) return BadRequest(new { error = "A user with the Staff role is required." });
        if (!await teachers.ExistsAsync(request.TeacherId)) return BadRequest(new { error = "Teacher not found." });
        var staff = new Staff { UserId = request.UserId, TeacherId = request.TeacherId, JobTitle = request.JobTitle };
        db.Staff.Add(staff); await db.SaveChangesAsync(ct); return Created($"/api/identity/staff/{staff.Id}", staff);
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
