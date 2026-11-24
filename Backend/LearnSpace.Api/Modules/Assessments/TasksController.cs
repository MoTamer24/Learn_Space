using LearnSpace.Api.Common;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSpace.Api.Modules.Assessments;

[ApiController]
[Route("api")]
public class TasksController(CourseTaskService tasks) : ControllerBase
{
    [HttpPost("courses/{courseId:guid}/tasks")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<CourseTaskDto>> Create(Guid courseId, CreateTaskRequest request)
    {
        var task = await tasks.CreateAsync(User.GetUserId(), courseId, request);
        return StatusCode(201, task);
    }

    [HttpGet("courses/{courseId:guid}/tasks")]
    [Authorize]
    public async Task<ActionResult<List<CourseTaskDto>>> List(Guid courseId)
        => await tasks.ListAsync(User.GetUserId(), courseId);

    /// <summary>Creates the submission (201) or replaces it before the deadline (200).</summary>
    [HttpPut("tasks/{taskId:guid}/submission")]
    [Authorize(Roles = AppRoles.Student)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SubmissionForStudentDto>> Submit(Guid taskId,IFormFile file)
    {
        var (submission, created) = await tasks.SubmitAsync(User.GetUserId(), taskId, file);
        return created ? StatusCode(201, submission) : Ok(submission);
    }

    [HttpGet("tasks/{taskId:guid}/submission/me")]
    [Authorize(Roles = AppRoles.Student)]
    public async Task<ActionResult<SubmissionForStudentDto>> GetMine(Guid taskId)
        => await tasks.GetMineAsync(User.GetUserId(), taskId);

    [HttpGet("tasks/{taskId:guid}/submissions")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<List<SubmissionForTeacherDto>>> ListSubmissions(Guid taskId)
        => await tasks.ListSubmissionsAsync(User.GetUserId(), taskId);

    [HttpGet("submissions/{submissionId:guid}/file")]
    [Authorize]
    public async Task<IActionResult> GetFile(Guid submissionId)
    {
        var (content, contentType, fileName) = await tasks.OpenFileAsync(User.GetUserId(), submissionId);
        return File(content, contentType, fileName);
    }

    [HttpPost("submissions/{submissionId:guid}/grade")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<SubmissionForTeacherDto>> Grade(Guid submissionId, GradeSubmissionRequest request)
        => await tasks.GradeAsync(User.GetUserId(), submissionId, request);
}
