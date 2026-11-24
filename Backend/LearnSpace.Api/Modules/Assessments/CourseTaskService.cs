using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Common.Storage;
using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Assessments;

public class CourseTaskService(
    AppDbContext db,
    ICourseService courses,
    IEnrollmentService enrollments,
    IStorageService storage,
    UploadValidator uploads,
    IUserLookup users)
{
    // =====================================================================
    // Task definition (teacher) and listing
    // =====================================================================

    public async Task<CourseTaskDto> CreateAsync(Guid teacherId, Guid courseId, CreateTaskRequest request)
    {
        await courses.RequireOwnerAsync(teacherId, courseId);

        if (request.MaxScore <= 0)
            throw AppException.BadRequest("MaxScore must be greater than 0.");

        var task = new CourseTask
        {
            CourseId = courseId,
            Title = request.Title.Trim(),
            Instructions = request.Instructions.Trim(),
            DeadlineAt = request.DeadlineAt.UtcDateTime,   // Npgsql timestamptz only accepts UTC
            MaxScore = request.MaxScore
        };
        db.CourseTasks.Add(task);
        await db.SaveChangesAsync();
        return ToDto(task);
    }

    public async Task<List<CourseTaskDto>> ListAsync(Guid userId, Guid courseId)
    {
        await enrollments.RequireCanViewCourseAsync(userId, courseId);

        var tasks = await db.CourseTasks.AsNoTracking()
            .Where(t => t.CourseId == courseId)
            .OrderBy(t => t.DeadlineAt)
            .ToListAsync();

        return tasks.Select(ToDto).ToList();
    }

    // =====================================================================
    // Student submission (rules 11 to 13)
    // =====================================================================

    public async Task<(SubmissionForStudentDto Submission, bool Created)> SubmitAsync(Guid studentId, Guid taskId, IFormFile file)
    {
        var task = await LoadTaskAsync(taskId);
        await enrollments.RequireCanViewCourseAsync(studentId, task.CourseId);   // only enrolled students (403)

        var now = DateTime.UtcNow;
        var existing = await db.TaskSubmissions.FirstOrDefaultAsync(s => s.TaskId == taskId && s.StudentId == studentId);

        // Rule 13: replacing is allowed only before the deadline and while not graded.
        if (existing is not null && (now > task.DeadlineAt || existing.Status != SubmissionStatus.Submitted))
            throw AppException.Conflict("This submission can no longer be replaced (deadline passed or already graded).");

        uploads.Validate(file, UploadKinds.TaskFile);

        // Save the file first, then the DB row. If the save fails nothing is written.
        await using var stream = file.OpenReadStream();
        var newKey = await storage.SaveAsync(stream, file.FileName);

        string? oldKey = null;
        TaskSubmission submission;
        var created = existing is null;

        try
        {
            if (existing is null)
            {
                // Rule 12: the first submission after the deadline is accepted but flagged late.
                submission = new TaskSubmission
                {
                    TaskId = taskId,
                    StudentId = studentId,
                    SubmittedAt = now,
                    IsLate = now > task.DeadlineAt,
                    StorageKey = newKey,
                    FileName = UploadValidator.CleanFileName(file.FileName),
                    ContentType = UploadValidator.ContentTypeFor(file.FileName),
                    Status = SubmissionStatus.Submitted
                };
                db.TaskSubmissions.Add(submission);
            }
            else
            {
                submission = existing;
                oldKey = existing.StorageKey;
                submission.StorageKey = newKey;
                submission.FileName = UploadValidator.CleanFileName(file.FileName);
                submission.ContentType = UploadValidator.ContentTypeFor(file.FileName);
                submission.SubmittedAt = now;
            }

            await db.SaveChangesAsync();
        }
        catch
        {
            await storage.TryDeleteAsync(newKey);
            throw;
        }

        if (oldKey is not null) await storage.TryDeleteAsync(oldKey);

        return (ToStudentDto(submission), created);
    }

    public async Task<SubmissionForStudentDto> GetMineAsync(Guid studentId, Guid taskId)
    {
        var submission = await db.TaskSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TaskId == taskId && s.StudentId == studentId)
            ?? throw AppException.NotFound("You have not submitted this task yet.");

        return ToStudentDto(submission);
    }

    // =====================================================================
    // Teacher side
    // =====================================================================

    public async Task<List<SubmissionForTeacherDto>> ListSubmissionsAsync(Guid teacherId, Guid taskId)
    {
        var task = await LoadTaskAsync(taskId);
        await courses.RequireOwnerAsync(teacherId, task.CourseId);

        var submissions = await db.TaskSubmissions.AsNoTracking()
            .Where(s => s.TaskId == taskId)
            .OrderBy(s => s.SubmittedAt)
            .ToListAsync();

        var names = await users.GetDisplayNamesAsync(submissions.Select(s => s.StudentId));
        return submissions.Select(s => ToTeacherDto(s, names.GetValueOrDefault(s.StudentId, ""))).ToList();
    }

    public async Task<SubmissionForTeacherDto> GradeAsync(Guid teacherId, Guid submissionId, GradeSubmissionRequest request)
    {
        var submission = await db.TaskSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw AppException.NotFound("Submission not found.");

        var task = await LoadTaskAsync(submission.TaskId);
        await courses.RequireOwnerAsync(teacherId, task.CourseId);

        if (request.Score < 0 || request.Score > task.MaxScore)
            throw AppException.BadRequest($"Score must be between 0 and {task.MaxScore}.");

        submission.Score = request.Score;
        submission.TeacherNote = string.IsNullOrWhiteSpace(request.TeacherNote) ? null : request.TeacherNote.Trim();
        submission.Status = SubmissionStatus.Graded;
        await db.SaveChangesAsync();

        var names = await users.GetDisplayNamesAsync([submission.StudentId]);
        return ToTeacherDto(submission, names.GetValueOrDefault(submission.StudentId, ""));
    }

    /// <summary>The submitting student or the owning teacher may download the file.</summary>
    public async Task<(Stream Content, string ContentType, string FileName)> OpenFileAsync(Guid userId, Guid submissionId)
    {
        var submission = await db.TaskSubmissions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw AppException.NotFound("Submission not found.");

        if (submission.StudentId != userId)
        {
            var task = await LoadTaskAsync(submission.TaskId);
            await courses.RequireOwnerAsync(userId, task.CourseId);
        }

        var stream = await storage.OpenReadAsync(submission.StorageKey);
        return (stream, submission.ContentType, submission.FileName);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private async Task<CourseTask> LoadTaskAsync(Guid taskId)
        => await db.CourseTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId)
            ?? throw AppException.NotFound("Task not found.");

    private static CourseTaskDto ToDto(CourseTask t)
        => new(t.Id, t.CourseId, t.Title, t.Instructions, t.DeadlineAt, t.MaxScore);

    // The student sees score and note only when Graded (rule 14).
    private static SubmissionForStudentDto ToStudentDto(TaskSubmission s)
    {
        var graded = s.Status == SubmissionStatus.Graded;
        return new SubmissionForStudentDto(
            s.Id, s.TaskId, s.SubmittedAt, s.IsLate, s.FileName, s.Status,
            graded ? s.Score : null, graded ? s.TeacherNote : null);
    }

    private static SubmissionForTeacherDto ToTeacherDto(TaskSubmission s, string studentName)
        => new(s.Id, s.TaskId, s.StudentId, studentName, s.SubmittedAt, s.IsLate, s.FileName, s.Status, s.Score, s.TeacherNote);
}
