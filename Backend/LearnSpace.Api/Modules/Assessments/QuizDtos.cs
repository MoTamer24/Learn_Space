using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Assessments;

// ---------- requests ----------

public record CreateOptionRequest(
    [property: Required, StringLength(500)] string Text,
    bool IsCorrect);

public record CreateQuestionRequest(
    QuestionType Type,
    [property: Required, StringLength(2000)] string Text,
    [property: Range(1, 1000)] int Points,
    List<CreateOptionRequest>? Options);

public record CreateQuizRequest(
    [property: Required, StringLength(200)] string Title,
    [property: Range(1, 480)] int DurationMinutes,
    List<CreateQuestionRequest>? Questions);

public record SaveAnswerRequest(
    Guid QuestionId,
    Guid? SelectedOptionId,
    [property: StringLength(10000)] string? TextAnswer);

public record GradeAnswerRequest(
    Guid AnswerId,
    decimal AwardedPoints,
    [property: StringLength(2000)] string? TeacherNote);

public record GradeAttemptRequest(
    [property: Required, MinLength(1)] List<GradeAnswerRequest> Answers);

// ---------- quiz definition (teacher sees IsCorrect) ----------

public record OptionForTeacherDto(Guid Id, string Text, bool IsCorrect);

public record QuestionForTeacherDto(
    Guid Id, QuestionType Type, string Text, int Points, int Position, List<OptionForTeacherDto> Options);

public record QuizForTeacherDto(
    Guid Id, Guid CourseId, string Title, int DurationMinutes, QuizStatus Status, List<QuestionForTeacherDto> Questions);

// Students only get a summary in the course listing, so the questions of a timed quiz
// are not visible before the attempt starts.
public record QuizSummaryForStudentDto(
    Guid Id, Guid CourseId, string Title, int DurationMinutes, int QuestionCount, int TotalPoints);

// ---------- attempt, as seen by the student ----------

public record OptionForStudentDto(Guid Id, string Text);   // no IsCorrect, ever

public record AttemptQuestionDto(
    Guid Id, QuestionType Type, string Text, int Points, int Position, List<OptionForStudentDto> Options);

public record SavedAnswerDto(Guid QuestionId, Guid? SelectedOptionId, string? TextAnswer);

public record AttemptStartDto(
    Guid AttemptId, Guid QuizId, string QuizTitle, AttemptStatus Status,
    DateTime StartedAt, DateTime DeadlineAt,
    List<AttemptQuestionDto> Questions, List<SavedAnswerDto> Answers);

/// <summary>Everything a student may see until the attempt is Graded (rule 10).</summary>
public record StudentAttemptStatusDto(
    Guid Id, Guid QuizId, AttemptStatus Status, DateTime StartedAt, DateTime DeadlineAt, DateTime? SubmittedAt);

public record StudentAnswerResultDto(
    Guid QuestionId, string QuestionText, int Points, decimal? AwardedPoints, string? TeacherNote);

public record StudentAttemptResultDto(
    Guid Id, Guid QuizId, AttemptStatus Status, DateTime? SubmittedAt, decimal? Score,
    List<StudentAnswerResultDto> Answers);

// ---------- attempt, as seen by the teacher ----------

public record TeacherAnswerDto(
    Guid? AnswerId, Guid QuestionId, QuestionType Type, string QuestionText, int Points,
    Guid? SelectedOptionId, string? SelectedOptionText, bool? IsCorrect,
    string? TextAnswer, decimal? AwardedPoints, string? TeacherNote);

public record TeacherAttemptDto(
    Guid Id, Guid QuizId, Guid StudentId, string StudentName, AttemptStatus Status,
    DateTime StartedAt, DateTime DeadlineAt, DateTime? SubmittedAt, decimal? Score,
    List<TeacherAnswerDto> Answers);

public record AttemptSummaryDto(
    Guid Id, Guid StudentId, string StudentName, AttemptStatus Status, decimal? Score,
    DateTime StartedAt, DateTime? SubmittedAt);
