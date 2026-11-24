namespace LearnSpace.Api.Modules.Catalog;

public enum CourseStatus { Draft, Published }

public class TeacherProfile
{
    public Guid UserId { get; set; }          // PK and FK to ApplicationUser
    public string Bio { get; set; } = "";
    public string? LogoKey { get; set; }
    public string AccentColor { get; set; } = "#0F766E";
}

public class Course
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Subject { get; set; } = "";
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
}
