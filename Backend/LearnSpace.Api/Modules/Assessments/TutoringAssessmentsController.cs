using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LearnSpace.Api.Data;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Groups;
using System.Security.Claims;

namespace LearnSpace.Api.Modules.Assessments;

[ApiController, Route("api/assessments"), Authorize]
public class TutoringAssessmentsController(TutoringAssessmentService service, AppDbContext db, IGroupBillingService groups, IStudentDebtService students) : ControllerBase
{
    [HttpGet, Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher + "," + AppRoles.Student)] public async Task<IActionResult> Get([FromQuery] Guid? groupId, CancellationToken ct)
    {
        if (User.IsInRole(AppRoles.Admin)) return Ok(await service.GetAssessmentsAsync(groupId, null, null, ct));
        if (User.IsInRole(AppRoles.Teacher)) return Ok(await service.GetAssessmentsAsync(groupId, CurrentUserId(), null, ct));
        var studentId = await students.GetStudentIdForUserAsync(CurrentUserId(), ct);
        if (studentId is null) return Ok(Array.Empty<Assessment>());
        return Ok(await service.GetAssessmentsAsync(groupId, null, await groups.GetActiveGroupIdsForStudentAsync(studentId.Value, ct), ct));
    }
    [HttpPost, Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)] public async Task<IActionResult> Create(CreateTutoringAssessmentRequest request, CancellationToken ct) { try { var assigned = request with { TeacherId = User.IsInRole(AppRoles.Admin) ? request.TeacherId : CurrentUserId() }; var a = await service.CreateAsync(assigned, ct); return Created($"/api/assessments/{a.Id}", a); } catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); } }
    [HttpGet("questions"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)] public async Task<IActionResult> Questions([FromQuery] Guid? teacherId, CancellationToken ct) => Ok(await service.GetQuestionBankAsync(teacherId, ct));
    [HttpGet("{id:guid}/questions"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher + "," + AppRoles.Student)] public async Task<IActionResult> AssessmentQuestions(Guid id, CancellationToken ct) { var assessment = await db.TutoringAssessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (assessment is null) return NotFound(); if (User.IsInRole(AppRoles.Teacher) && !await groups.IsGroupOwnedByTeacherAsync(CurrentUserId(), assessment.GroupId, ct)) return Forbid(); if (User.IsInRole(AppRoles.Student)) { var studentId = await students.GetStudentIdForUserAsync(CurrentUserId(), ct); if (studentId is null || !await groups.IsStudentActiveInGroupAsync(studentId.Value, assessment.GroupId, ct)) return Forbid(); } try { return Ok(await service.GetAssessmentQuestionsAsync(id, ct)); } catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); } }
    [HttpPost("questions"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)] public async Task<IActionResult> AddQuestion(CreateQuestionBankRequest request, CancellationToken ct) { try { var assigned = request with { TeacherId = User.IsInRole(AppRoles.Admin) ? request.TeacherId : CurrentUserId() }; return Ok(await service.AddQuestionAsync(assigned, ct)); } catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); } }
    [HttpPost("{id:guid}/questions"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)] public async Task<IActionResult> AddQuestion(Guid id, AddAssessmentQuestionRequest request, CancellationToken ct) { var assessment = await db.TutoringAssessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (assessment is null) return NotFound(); if (User.IsInRole(AppRoles.Teacher) && !await groups.IsGroupOwnedByTeacherAsync(CurrentUserId(), assessment.GroupId, ct)) return Forbid(); try { return Ok(await service.AddQuestionToAssessmentAsync(id, request.QuestionId, request.QuestionOrder, request.Mark, ct)); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    [HttpPost("{id:guid}/submissions"), Authorize(Roles = AppRoles.Student)] public async Task<IActionResult> Submit(Guid id, SubmitTutoringAssessmentRequest request, CancellationToken ct) { try { var studentId = await students.GetStudentIdForUserAsync(CurrentUserId(), ct); if (studentId is null) return Forbid(); return Ok(await service.SubmitAsync(id, studentId.Value, request.Answers, ct)); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    [HttpGet("{id:guid}/submissions")] public async Task<IActionResult> Submissions(Guid id, CancellationToken ct) { if (!User.IsInRole(AppRoles.Admin) && !await db.TutoringAssessments.AnyAsync(x => x.Id == id && x.TeacherId == CurrentUserId(), ct)) return NotFound(); return Ok(await db.StudentAssessments.AsNoTracking().Where(x => x.AssessmentId == id).ToListAsync(ct)); }
    [HttpPost("submissions/{id:guid}/grade"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)] public async Task<IActionResult> Grade(Guid id, GradeTutoringAssessmentRequest request, CancellationToken ct) { try { return Ok(await service.GradeAsync(id, User.IsInRole(AppRoles.Admin) ? request.TeacherId : CurrentUserId(), request.Answers, ct)); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
