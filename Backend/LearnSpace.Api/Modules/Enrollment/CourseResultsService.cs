using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Enrollments;

/// <summary>Builds the teacher's list of students enrolled in one of their courses.</summary>
public class CourseResultsService(
    AppDbContext db,
    ICourseService courses,
    IUserLookup users)
{
    public async Task<List<StudentResultRowDto>> GetAsync(Guid teacherId, Guid courseId)
    {
        await courses.RequireOwnerAsync(teacherId, courseId);

        var studentIds = await db.Enrollments.AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var names = await users.GetDisplayNamesAsync(studentIds);

        return studentIds
            .Select(id => new StudentResultRowDto(id, names.GetValueOrDefault(id, "")))
            .OrderBy(r => r.DisplayName)
            .ToList();
    }
}
