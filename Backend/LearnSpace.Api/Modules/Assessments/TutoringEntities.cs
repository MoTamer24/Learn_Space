namespace LearnSpace.Api.Modules.Assessments;

public enum TutoringQuestionType { MCQ, Essay }
public enum AssessmentType { Exam, Quiz }

public class QuestionBankItem
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SubjectLevelId { get; set; }
    public string QuestionText { get; set; } = "";
    public TutoringQuestionType Type { get; set; }
    public string? ModelAnswer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class QuestionBankOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string OptionText { get; set; } = "";
    public bool IsCorrect { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Assessment
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid GroupId { get; set; }
    public Guid? SessionId { get; set; }
    public string Title { get; set; } = "";
    public AssessmentType Type { get; set; }
    public DateTime Date { get; set; }
    public int Duration { get; set; }
    public decimal TotalMarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AssessmentQuestion
{
    public Guid AssessmentId { get; set; }
    public Guid QuestionId { get; set; }
    public int QuestionOrder { get; set; }
    public decimal Mark { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StudentAssessment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid AssessmentId { get; set; }
    public decimal? Mark { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StudentAnswer
{
    public Guid Id { get; set; }
    public Guid StudentAssessmentId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? AnswerText { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? MarkAwarded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
