using LearnSpace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Assessments;

public record StudentAssessmentSummary(
    string QuizStatus, decimal? QuizScore, string TaskStatus, bool? TaskIsLate, decimal? TaskScore)
{
    public static readonly StudentAssessmentSummary Empty = new("NotStarted", null, "NotSubmitted", null, null);
}

/// <summary>What the Enrollment module may ask the Assessments module (for the teacher's results table).</summary>
public interface IAssessmentSummaryService
{
    Task<Dictionary<Guid, StudentAssessmentSummary>> GetForCourseAsync(Guid courseId, IReadOnlyCollection<Guid> studentIds);
}

public class AssessmentSummaryService(AppDbContext db, QuizService quizzes) : IAssessmentSummaryService
{
    public async Task<Dictionary<Guid, StudentAssessmentSummary>> GetForCourseAsync(
        Guid courseId, IReadOnlyCollection<Guid> studentIds)
    {
        var quizIds = await db.Quizzes
            .Where(q => q.CourseId == courseId && q.Status == QuizStatus.Published)
            .Select(q => q.Id)
            .ToListAsync();

        var taskIds = await db.CourseTasks
            .Where(t => t.CourseId == courseId)
            .Select(t => t.Id)
            .ToListAsync();

        // Rule 7: an attempt that ran out of time is finalized the moment somebody reads it.
        var now = DateTime.UtcNow;
        var expiredIds = await db.QuizAttempts
            .Where(a => quizIds.Contains(a.QuizId) && a.Status == AttemptStatus.InProgress && a.DeadlineAt < now)
            .Select(a => a.Id)
            .ToListAsync();
        foreach (var id in expiredIds) await quizzes.EnsureAttemptCurrentAsync(id);

        var attempts = await db.QuizAttempts.AsNoTracking()
            .Where(a => quizIds.Contains(a.QuizId) && studentIds.Contains(a.StudentId))
            .ToListAsync();

        var submissions = await db.TaskSubmissions.AsNoTracking()
            .Where(s => taskIds.Contains(s.TaskId) && studentIds.Contains(s.StudentId))
            .ToListAsync();

        var result = new Dictionary<Guid, StudentAssessmentSummary>();
        foreach (var studentId in studentIds)
        {
            // If the course has several quizzes/tasks, the table shows the student's most recent one.
            var attempt = attempts.Where(a => a.StudentId == studentId).OrderByDescending(a => a.StartedAt).FirstOrDefault();
            var submission = submissions.Where(s => s.StudentId == studentId).OrderByDescending(s => s.SubmittedAt).FirstOrDefault();

            result[studentId] = new StudentAssessmentSummary(
                attempt?.Status.ToString() ?? "NotStarted",
                attempt?.Score,
                submission?.Status.ToString() ?? "NotSubmitted",
                submission?.IsLate,
                submission?.Score);
        }
        return result;
    }
}
