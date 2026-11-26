using LearnSpace.Api.Modules.Attendance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;
using System.Security.Claims;

namespace LearnSpace.Api.Modules.Groups;

[ApiController, Route("api/groups"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher + "," + AppRoles.Staff)]
public class GroupsController(GroupService groups, AttendanceService attendance, IGroupBillingService groupDirectory, IStaffDirectory staff) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Ok(await groups.GetGroupsAsync(User.IsInRole(AppRoles.Admin) ? null : CurrentUserId(), ct));
    [HttpPost] public async Task<IActionResult> Create(CreateStudyGroupRequest request, CancellationToken ct)
    { try { var group = await groups.CreateGroupAsync(request with { TeacherId = User.IsInRole(AppRoles.Admin) ? request.TeacherId : await CurrentTeacherIdAsync(ct) }, ct); return Created($"/api/groups/{group.Id}", group); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    [HttpGet("{groupId:guid}/enrollments")] public async Task<IActionResult> Enrollments(Guid groupId, CancellationToken ct) { if (!await CanAccessGroup(groupId, ct)) return Forbid(); return Ok(await groups.GetEnrollmentsAsync(groupId, ct)); }
    [HttpPost("{groupId:guid}/enrollments")] public async Task<IActionResult> Enroll(Guid groupId, CreateEnrollmentRequest request, CancellationToken ct)
    { if (!await CanAccessGroup(groupId, ct)) return Forbid(); try { var enrollment = await groups.EnrollAsync(groupId, request, ct); return Created($"/api/groups/{groupId}/enrollments/{enrollment.Id}", enrollment); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    [HttpPost("{groupId:guid}/schedules")] public async Task<IActionResult> Schedule(Guid groupId, CreateScheduleRequest request, CancellationToken ct)
    { if (!await CanAccessGroup(groupId, ct)) return Forbid(); try { return Ok(await groups.AddScheduleAsync(groupId, request, ct)); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    [HttpGet("{groupId:guid}/sessions")] public async Task<IActionResult> Sessions(Guid groupId, CancellationToken ct) { if (!await CanAccessGroup(groupId, ct)) return Forbid(); return Ok(await groups.GetSessionsAsync(groupId, ct)); }
    [HttpGet("{groupId:guid}/schedules")] public async Task<IActionResult> Schedules(Guid groupId, CancellationToken ct) { if (!await CanAccessGroup(groupId, ct)) return Forbid(); return Ok(await groups.GetSchedulesAsync(groupId, ct)); }
    [HttpPost("{groupId:guid}/sessions")] public async Task<IActionResult> Session(Guid groupId, CreateSessionRequest request, CancellationToken ct)
    { if (!await CanAccessGroup(groupId, ct)) return Forbid(); try { return Ok(await groups.AddSessionAsync(groupId, request, ct)); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    [HttpPost("check-in")] public async Task<IActionResult> CheckIn(AttendanceCheckInRequest request, CancellationToken ct)
    { if (!User.IsInRole(AppRoles.Admin) && !await groupDirectory.IsSessionOwnedByTeacherAsync(await CurrentTeacherIdAsync(ct), request.SessionId, ct)) return Forbid(); var result = await attendance.CheckInAsync(request.StudentId, request.SessionId, request.Status, ct); return result.Allowed ? Ok(result) : Conflict(result); }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
    private async Task<bool> CanAccessGroup(Guid groupId, CancellationToken ct) => User.IsInRole(AppRoles.Admin) || await groupDirectory.IsGroupOwnedByTeacherAsync(await CurrentTeacherIdAsync(ct), groupId, ct);
    private async Task<Guid> CurrentTeacherIdAsync(CancellationToken ct) => User.IsInRole(AppRoles.Staff) ? await staff.GetTeacherIdForStaffUserAsync(CurrentUserId(), ct) ?? Guid.Empty : CurrentUserId();
}
