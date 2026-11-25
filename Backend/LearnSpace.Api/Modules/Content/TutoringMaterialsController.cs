using LearnSpace.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;

namespace LearnSpace.Api.Modules.Content;

public record CreateTutoringMaterialRequest(Guid TeacherId, Guid? GroupId, Guid? SessionId, string Title, TutoringMaterialType Type, string Url, decimal SizeMB);

[ApiController, Route("api/tutoring/materials"), Authorize]
public class TutoringMaterialsController(AppDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get([FromQuery] Guid? teacherId, [FromQuery] Guid? groupId, CancellationToken ct)
    { if (!User.IsInRole(AppRoles.Admin)) teacherId = CurrentUserId(); var q = db.TutoringMaterials.AsNoTracking(); if (teacherId.HasValue) q = q.Where(x => x.TeacherId == teacherId); if (groupId.HasValue) q = q.Where(x => x.GroupId == groupId); return Ok(await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct)); }
    [HttpPost, Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)] public async Task<IActionResult> Create(CreateTutoringMaterialRequest request, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Url) || request.SizeMB < 0 || (request.GroupId is null && request.SessionId is null)) return BadRequest(new { error = "Title, URL, non-negative size, and group or session are required." }); var item = new TutoringMaterial { TeacherId = User.IsInRole(AppRoles.Admin) ? request.TeacherId : CurrentUserId(), GroupId = request.GroupId, SessionId = request.SessionId, Title = request.Title.Trim(), Type = request.Type, Url = request.Url.Trim(), SizeMB = request.SizeMB }; db.TutoringMaterials.Add(item); await db.SaveChangesAsync(ct); return Created($"/api/tutoring/materials/{item.Id}", item); }
    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
