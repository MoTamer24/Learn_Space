namespace LearnSpace.Api.Modules.Identity;

public class Student
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Code { get; set; } = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Parent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StudentParent
{
    public Guid StudentId { get; set; }
    public Guid ParentId { get; set; }
    public string Relation { get; set; } = "Parent";
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Staff
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TeacherId { get; set; }
    public string JobTitle { get; set; } = "Assistant";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Identity-module contract used by other modules; callers never query Identity tables directly.</summary>
public interface IStudentDirectory
{
    Task<bool> StudentExistsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Guid?> GetStudentIdForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetStudentIdsForParentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IStaffDirectory
{
    Task<Guid?> GetTeacherIdForStaffUserAsync(Guid staffUserId, CancellationToken cancellationToken = default);
}
