using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Groups;

namespace LearnSpace.Api.Modules.Attendance;

[ApiController, Route("api/attendance"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher + "," + AppRoles.Staff)]
public class AttendanceController(AttendanceService service, IGroupBillingService groups, IStaffDirectory staff) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get([FromQuery] Guid? sessionId, CancellationToken ct)
    {
        var records = await service.GetAsync(sessionId, ct);
        if (User.IsInRole(AppRoles.Admin)) return Ok(records);
        var visible = new List<Attendance>();
        var userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
        if (User.IsInRole(AppRoles.Staff)) userId = await staff.GetTeacherIdForStaffUserAsync(userId, ct) ?? Guid.Empty;
        foreach (var record in records) if (await groups.IsSessionOwnedByTeacherAsync(userId, record.SessionId, ct)) visible.Add(record);
        return Ok(visible);
    }
}
