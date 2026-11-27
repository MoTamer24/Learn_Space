using LearnSpace.Api.Common;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSpace.Api.Modules.Enrollments;

[ApiController]
[Route("api")]
public class EnrollmentController(EnrollmentService enrollments, CourseResultsService results) : ControllerBase
{
    /// <summary>201 for a new enrollment, 200 if the student was already enrolled.</summary>
    [HttpPost("courses/{courseId:guid}/enroll")]
    [Authorize(Roles = AppRoles.Student)]
    public async Task<ActionResult<EnrollmentDto>> Enroll(Guid courseId)
    {
        var (enrollment, created) = await enrollments.EnrollAsync(User.GetUserId(), courseId);
        return created ? StatusCode(201, enrollment) : Ok(enrollment);
    }

    [HttpGet("enrollments/me")]
    [Authorize(Roles = AppRoles.Student)]
    public async Task<ActionResult<List<MyEnrollmentDto>>> Mine()
        => await enrollments.GetMineAsync(User.GetUserId());

    /// <summary>The teacher's list of enrolled students.</summary>
    [HttpGet("courses/{courseId:guid}/students")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<List<StudentResultRowDto>>> Students(Guid courseId)
        => await results.GetAsync(User.GetUserId(), courseId);
}
