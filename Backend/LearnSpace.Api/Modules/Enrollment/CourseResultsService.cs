using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Assessments;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Enrollments;

/// <summary>
/// Builds the teacher's results table. Lives in its own class (not EnrollmentService) so that
/// EnrollmentService stays free of dependencies on Assessments, which itself uses IEnrollmentService.
/// </summary>
public class CourseResultsService(
    AppDbContext db,
    ICourseService courses,
    IUserLookup users,
    IAssessmentSummaryService summaries)
{
    public async Task<List<StudentResultRowDto>> GetAsync(Guid teacherId, Guid courseId)
    {
        await courses.RequireOwnerAsync(teacherId, courseId);

        var studentIds = await db.Enrollments.AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var names = await users.GetDisplayNamesAsync(studentIds);
        var results = await summaries.GetForCourseAsync(courseId, studentIds);

        return studentIds
            .Select(id =>
            {
                var s = results.GetValueOrDefault(id) ?? StudentAssessmentSummary.Empty;
                return new StudentResultRowDto(
                    id, names.GetValueOrDefault(id, ""),
                    s.QuizStatus, s.QuizScore, s.TaskStatus, s.TaskIsLate, s.TaskScore);
            })
            .OrderBy(r => r.DisplayName)
            .ToList();
    }
}
