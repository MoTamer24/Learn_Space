using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Enrollments;

/// <summary>The part of the Enrollment module that other modules are allowed to call.</summary>
public interface IEnrollmentService
{
    Task<bool> IsEnrolledAsync(Guid userId, Guid courseId);

    /// <summary>True when the user owns the course OR is enrolled in it.</summary>
    Task<bool> CanViewCourseAsync(Guid userId, Guid courseId);

    /// <summary>404 if the course does not exist, 403 if the user is neither owner nor enrolled.</summary>
    Task RequireCanViewCourseAsync(Guid userId, Guid courseId);
}

public class EnrollmentService(AppDbContext db, ICourseService courses) : IEnrollmentService
{
    public Task<bool> IsEnrolledAsync(Guid userId, Guid courseId)
        => db.Enrollments.AnyAsync(e => e.CourseId == courseId && e.StudentId == userId);

    public async Task<bool> CanViewCourseAsync(Guid userId, Guid courseId)
        => await courses.IsOwnerAsync(userId, courseId) || await IsEnrolledAsync(userId, courseId);

    public async Task RequireCanViewCourseAsync(Guid userId, Guid courseId)
    {
        if (await courses.GetStatusAsync(courseId) is null)
            throw AppException.NotFound("Course not found.");

        if (!await CanViewCourseAsync(userId, courseId))
            throw AppException.Forbidden("You must be enrolled in this course.");
    }

    /// <summary>Idempotent: enrolling twice returns the existing enrollment (Created = false).</summary>
    public async Task<(EnrollmentDto Enrollment, bool Created)> EnrollAsync(Guid studentId, Guid courseId)
    {
        var status = await courses.GetStatusAsync(courseId)
            ?? throw AppException.NotFound("Course not found.");

        if (status != CourseStatus.Published)
            throw AppException.Conflict("You can only enroll in a published course.");

        var existing = await db.Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
        if (existing is not null) return (ToDto(existing), false);

        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = studentId,
            EnrolledAt = DateTime.UtcNow
        };
        db.Enrollments.Add(enrollment);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two clicks at the same time: the unique index rejected the second one. Return the winner.
            db.Entry(enrollment).State = EntityState.Detached;
            var winner = await db.Enrollments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
            if (winner is null) throw;
            return (ToDto(winner), false);
        }

        return (ToDto(enrollment), true);
    }

    public async Task<List<MyEnrollmentDto>> GetMineAsync(Guid studentId)
    {
        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var coursesById = (await courses.GetByIdsAsync(enrollments.Select(e => e.CourseId)))
            .ToDictionary(c => c.Id);

        return enrollments
            .Where(e => coursesById.ContainsKey(e.CourseId))
            .Select(e => new MyEnrollmentDto(e.Id, e.EnrolledAt, coursesById[e.CourseId]))
            .ToList();
    }

    private static EnrollmentDto ToDto(Enrollment e) => new(e.Id, e.CourseId, e.StudentId, e.EnrolledAt);
}
