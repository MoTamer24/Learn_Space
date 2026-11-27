namespace LearnSpace.Api.Modules.Groups;

public enum GroupStatus { Active, Paused, Archived }
public enum PriceType { Monthly, Session }
public enum EnrollmentStatus { Active, Paused, Completed }
public enum ClassSessionStatus { Done, Cancelled, Postponed, Scheduled }

public class StudyGroup
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SubjectLevelId { get; set; }
    public string Name { get; set; } = "";
    public GroupStatus Status { get; set; } = GroupStatus.Active;
    public int Capacity { get; set; } = 20;
    public decimal Price { get; set; }
    public PriceType PriceType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class GroupSchedule
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class GroupEnrollment
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid GroupId { get; set; }
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EndDate { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ClassSession
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMin { get; set; } = 60;
    public ClassSessionStatus Status { get; set; } = ClassSessionStatus.Scheduled;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Group-module contract used by other modules; callers never query Group tables directly.</summary>
public interface IGroupDirectory
{
    Task<bool> IsStudentActiveInGroupAsync(Guid studentId, Guid groupId, CancellationToken ct = default);
    Task<bool> IsGroupOwnedByTeacherAsync(Guid teacherId, Guid groupId, CancellationToken ct = default);
    Task<bool> IsSessionInGroupAsync(Guid sessionId, Guid groupId, CancellationToken ct = default);
    Task<bool> IsSessionOwnedByTeacherAsync(Guid teacherId, Guid sessionId, CancellationToken ct = default);
    Task<List<Guid>> GetActiveGroupIdsForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<List<Guid>> GetActiveStudentIdsForTeacherAsync(Guid teacherId, CancellationToken ct = default);
}
