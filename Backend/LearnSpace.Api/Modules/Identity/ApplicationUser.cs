using Microsoft.AspNetCore.Identity;

namespace LearnSpace.Api.Modules.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class AppRoles
{
    public const string Student = "Student";
    public const string Teacher = "Teacher";
    public const string Admin = "Admin";
    public const string Parent = "Parent";
    public const string Staff = "Staff";

    public static readonly string[] All = [Admin, Teacher, Student, Parent, Staff];
}
