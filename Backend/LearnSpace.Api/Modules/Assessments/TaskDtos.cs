using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Assessments;

public record CreateTaskRequest(
    [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(5000)] string Instructions,
    DateTimeOffset DeadlineAt,          // send with a "Z" or an offset, e.g. 2026-12-31T20:00:00Z
    decimal MaxScore);

public record GradeSubmissionRequest(
    decimal Score,
    [property: StringLength(2000)] string? TeacherNote);

public record CourseTaskDto(
    Guid Id, Guid CourseId, string Title, string Instructions, DateTime DeadlineAt, decimal MaxScore);

/// <summary>Score and note stay null until the submission is Graded.</summary>
public record SubmissionForStudentDto(
    Guid Id, Guid TaskId, DateTime SubmittedAt, bool IsLate, string FileName,
    SubmissionStatus Status, decimal? Score, string? TeacherNote);

public record SubmissionForTeacherDto(
    Guid Id, Guid TaskId, Guid StudentId, string StudentName, DateTime SubmittedAt, bool IsLate,
    string FileName, SubmissionStatus Status, decimal? Score, string? TeacherNote);
