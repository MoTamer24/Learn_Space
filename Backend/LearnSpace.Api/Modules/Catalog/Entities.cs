namespace LearnSpace.Api.Modules.Catalog;

public enum CourseStatus { Draft, Published }

public class TeacherProfile
{
    public Guid UserId { get; set; }          // PK and FK to ApplicationUser
    public string Bio { get; set; } = "";
    public string? LogoKey { get; set; }
    public string AccentColor { get; set; } = "#0F766E";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Course
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Subject { get; set; } = "";
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Subject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Level
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SubjectLevel
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public Guid LevelId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
