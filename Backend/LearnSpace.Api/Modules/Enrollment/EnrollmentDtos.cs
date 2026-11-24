using LearnSpace.Api.Modules.Catalog;

namespace LearnSpace.Api.Modules.Enrollments;

public record EnrollmentDto(Guid Id, Guid CourseId, Guid StudentId, DateTime EnrolledAt);

public record MyEnrollmentDto(Guid EnrollmentId, DateTime EnrolledAt, CourseDto Course);

/// <summary>One row of the teacher's results table.</summary>
public record StudentResultRowDto(
    Guid StudentId,
    string DisplayName,
    string QuizStatus,      // NotStarted | InProgress | PendingGrading | Graded
    decimal? QuizScore,
    string TaskStatus,      // NotSubmitted | Submitted | Graded
    bool? TaskIsLate,
    decimal? TaskScore);
