namespace LearnSpace.Api.Modules.Assessments;

public enum QuizStatus { Draft, Published }
public enum QuestionType { Mcq, Written }
public enum AttemptStatus { InProgress, PendingGrading, Graded }
public enum SubmissionStatus { Submitted, Graded }

public class Quiz
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public QuizStatus Status { get; set; } = QuizStatus.Draft;
    public List<Question> Questions { get; set; } = [];
}

public class Question
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public QuestionType Type { get; set; }
    public string Text { get; set; } = "";
    public int Points { get; set; }
    public int Position { get; set; }
    public List<QuestionOption> Options { get; set; } = [];
}

public class QuestionOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}

public class QuizAttempt
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
    public decimal? Score { get; set; }
    public List<AttemptAnswer> Answers { get; set; } = [];
}

public class AttemptAnswer
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextAnswer { get; set; }
    public decimal? AwardedPoints { get; set; }
    public string? TeacherNote { get; set; }
}

/// <summary>Named CourseTask to avoid clashing with System.Threading.Tasks.Task.</summary>
public class CourseTask
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public DateTime DeadlineAt { get; set; }
    public decimal MaxScore { get; set; }
}

public class TaskSubmission
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool IsLate { get; set; }
    public string StorageKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;
    public decimal? Score { get; set; }
    public string? TeacherNote { get; set; }
}
