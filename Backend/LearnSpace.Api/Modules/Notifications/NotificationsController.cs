using LearnSpace.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;

namespace LearnSpace.Api.Modules.Notifications;

[ApiController, Route("api/notifications"), Authorize]
public class NotificationsController(AppDbContext db, NotificationService service) : ControllerBase
{
    [HttpGet("teachers/{teacherId:guid}")] public async Task<IActionResult> Get(Guid teacherId, CancellationToken ct) { if (!User.IsInRole(AppRoles.Admin) && CurrentUserId() != teacherId) return Forbid(); return Ok(await service.GetForTeacherAsync(teacherId, ct)); }
    [HttpGet("recipients/{userId:guid}")] public async Task<IActionResult> ForRecipient(Guid userId, CancellationToken ct) { if (!User.IsInRole(AppRoles.Admin) && CurrentUserId() != userId) return Forbid(); return Ok(await db.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.SentAt).ToListAsync(ct)); }
    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
