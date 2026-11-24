using Microsoft.AspNetCore.Identity;

namespace LearnSpace.Api.Modules.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
}

public static class AppRoles
{
    public const string Student = "Student";
    public const string Teacher = "Teacher";

    public static readonly string[] All = [Student, Teacher];
}
