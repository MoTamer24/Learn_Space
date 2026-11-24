using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Catalog;

/// <summary>The part of the Catalog module that other modules are allowed to call.</summary>
public interface ICourseService
{
    Task<bool> IsOwnerAsync(Guid userId, Guid courseId);

    /// <summary>404 if the course does not exist, 403 if it belongs to someone else.</summary>
    Task RequireOwnerAsync(Guid userId, Guid courseId);

    /// <summary>Null when the course does not exist.</summary>
    Task<CourseStatus?> GetStatusAsync(Guid courseId);

    Task<List<CourseDto>> GetByIdsAsync(IEnumerable<Guid> courseIds);
}

public class CourseService(AppDbContext db, IUserLookup users) : ICourseService
{
    // ---------- used by other modules ----------

    public Task<bool> IsOwnerAsync(Guid userId, Guid courseId)
        => db.Courses.AnyAsync(c => c.Id == courseId && c.TeacherId == userId);

    public async Task RequireOwnerAsync(Guid userId, Guid courseId)
    {
        var teacherId = await db.Courses
            .Where(c => c.Id == courseId)
            .Select(c => (Guid?)c.TeacherId)
            .FirstOrDefaultAsync();

        if (teacherId is null) throw AppException.NotFound("Course not found.");
        if (teacherId != userId) throw AppException.Forbidden("This course belongs to another teacher.");
    }

    public async Task<CourseStatus?> GetStatusAsync(Guid courseId)
        => await db.Courses
            .Where(c => c.Id == courseId)
            .Select(c => (CourseStatus?)c.Status)
            .FirstOrDefaultAsync();

    public async Task<List<CourseDto>> GetByIdsAsync(IEnumerable<Guid> courseIds)
    {
        var ids = courseIds.Distinct().ToList();
        var courses = await db.Courses.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
        return await ToDtosAsync(courses);
    }

    // ---------- used by the Catalog controllers ----------

    public async Task<List<CourseDto>> ListPublishedAsync(string? subject)
    {
        var query = db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Published);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            var wanted = subject.Trim().ToLower();
            query = query.Where(c => c.Subject.ToLower() == wanted);
        }

        return await ToDtosAsync(await query.OrderBy(c => c.Title).ToListAsync());
    }

    public async Task<List<CourseDto>> ListPublishedByTeacherAsync(Guid teacherId)
    {
        var courses = await db.Courses.AsNoTracking()
            .Where(c => c.TeacherId == teacherId && c.Status == CourseStatus.Published)
            .OrderBy(c => c.Title)
            .ToListAsync();
        return await ToDtosAsync(courses);
    }

    public async Task<List<CourseDto>> ListMineAsync(Guid teacherId)
    {
        var courses = await db.Courses.AsNoTracking()
            .Where(c => c.TeacherId == teacherId)
            .OrderBy(c => c.Title)
            .ToListAsync();
        return await ToDtosAsync(courses);
    }

    /// <param name="userId">Null for anonymous callers.</param>
    public async Task<CourseDto> GetAsync(Guid courseId, Guid? userId)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId)
            ?? throw AppException.NotFound("Course not found.");

        // Drafts are invisible to everyone except the owner (answer 404 so their existence is not leaked).
        if (course.Status == CourseStatus.Draft && course.TeacherId != userId)
            throw AppException.NotFound("Course not found.");

        return (await ToDtosAsync([course]))[0];
    }

    public async Task<CourseDto> CreateAsync(Guid teacherId, CreateCourseRequest request)
    {
        var course = new Course
        {
            TeacherId = teacherId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Subject = request.Subject.Trim(),
            Status = CourseStatus.Draft
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return (await ToDtosAsync([course]))[0];
    }

    public async Task<CourseDto> UpdateAsync(Guid teacherId, Guid courseId, UpdateCourseRequest request)
    {
        var course = await LoadOwnedAsync(teacherId, courseId);
        course.Title = request.Title.Trim();
        course.Description = request.Description.Trim();
        course.Subject = request.Subject.Trim();
        await db.SaveChangesAsync();
        return (await ToDtosAsync([course]))[0];
    }

    public async Task<CourseDto> PublishAsync(Guid teacherId, Guid courseId)
    {
        var course = await LoadOwnedAsync(teacherId, courseId);
        if (course.Status != CourseStatus.Published)
        {
            course.Status = CourseStatus.Published;
            await db.SaveChangesAsync();
        }
        return (await ToDtosAsync([course]))[0];
    }

    // ---------- helpers ----------

    private async Task<Course> LoadOwnedAsync(Guid teacherId, Guid courseId)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId)
            ?? throw AppException.NotFound("Course not found.");
        if (course.TeacherId != teacherId)
            throw AppException.Forbidden("This course belongs to another teacher.");
        return course;
    }

    private async Task<List<CourseDto>> ToDtosAsync(List<Course> courses)
    {
        var names = await users.GetDisplayNamesAsync(courses.Select(c => c.TeacherId));
        return courses
            .Select(c => new CourseDto(c.Id, c.TeacherId, names.GetValueOrDefault(c.TeacherId, ""),
                c.Title, c.Description, c.Subject, c.Status))
            .ToList();
    }
}
