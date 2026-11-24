using LearnSpace.Api.Common;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSpace.Api.Modules.Assessments;

[ApiController]
[Route("api")]
public class QuizzesController(QuizService quizzes) : ControllerBase
{
    // ---------- quiz definition ----------

    [HttpPost("courses/{courseId:guid}/quizzes")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<QuizForTeacherDto>> Create(Guid courseId, CreateQuizRequest request)
    {
        var quiz = await quizzes.CreateAsync(User.GetUserId(), courseId, request);
        return StatusCode(201, quiz);
    }

    [HttpPost("quizzes/{quizId:guid}/publish")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<QuizForTeacherDto>> Publish(Guid quizId)
        => await quizzes.PublishAsync(User.GetUserId(), quizId);

    /// <summary>Teachers (owner) get full quizzes; enrolled students get a summary without questions or IsCorrect.</summary>
    [HttpGet("courses/{courseId:guid}/quizzes")]
    [Authorize]
    public async Task<IActionResult> List(Guid courseId)
    {
        var userId = User.GetUserId();
        if (User.IsInRole(AppRoles.Teacher))
            return Ok(await quizzes.ListForTeacherAsync(userId, courseId));

        return Ok(await quizzes.ListForStudentAsync(userId, courseId));
    }

    // ---------- taking the quiz ----------

    /// <summary>201 for a new attempt, 200 when resuming an attempt that is still in progress.</summary>
    [HttpPost("quizzes/{quizId:guid}/attempts")]
    [Authorize(Roles = AppRoles.Student)]
    public async Task<ActionResult<AttemptStartDto>> Start(Guid quizId)
    {
        var (attempt, created) = await quizzes.StartAsync(User.GetUserId(), quizId);
        return created ? StatusCode(201, attempt) : Ok(attempt);
    }

    [HttpPut("attempts/{attemptId:guid}/answers")]
    [Authorize(Roles = AppRoles.Student)]
    public async Task<ActionResult<StudentAttemptStatusDto>> SaveAnswers(Guid attemptId, List<SaveAnswerRequest> answers)
        => await quizzes.SaveAnswersAsync(User.GetUserId(), attemptId, answers);

    [HttpPost("attempts/{attemptId:guid}/submit")]
    [Authorize(Roles = AppRoles.Student)]
    public async Task<IActionResult> Submit(Guid attemptId)
        => Ok(await quizzes.SubmitAsync(User.GetUserId(), attemptId));

    /// <summary>Students get status only until Graded; the owning teacher gets full detail.</summary>
    [HttpGet("attempts/{attemptId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetAttempt(Guid attemptId)
    {
        var userId = User.GetUserId();
        if (User.IsInRole(AppRoles.Teacher))
            return Ok(await quizzes.GetForTeacherAsync(userId, attemptId));

        return Ok(await quizzes.GetForStudentAsync(userId, attemptId));
    }

    // ---------- teacher: results and grading ----------

    [HttpGet("quizzes/{quizId:guid}/attempts")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<List<AttemptSummaryDto>>> ListAttempts(Guid quizId)
        => await quizzes.ListAttemptsAsync(User.GetUserId(), quizId);

    [HttpPost("attempts/{attemptId:guid}/grade")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<TeacherAttemptDto>> Grade(Guid attemptId, GradeAttemptRequest request)
        => await quizzes.GradeAsync(User.GetUserId(), attemptId, request);
}
