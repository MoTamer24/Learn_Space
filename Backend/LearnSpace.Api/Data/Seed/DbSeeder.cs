using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Groups;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Data.Seed;

/// <summary>Development-only demo data. Runs once: it does nothing if any user already exists.</summary>
public class DbSeeder(AppDbContext db, UserManager<ApplicationUser> users)
{
    /// <summary>The same password for every seeded account (documented in the README).</summary>
    public const string DevPassword = "Dev@12345";

    public async Task SeedAsync()
    {
        if (await db.Users.AnyAsync()) return;

        // ---------- users ----------
        var teacher1 = await CreateUserAsync("ahmed.teacher@learnspace.dev", "Dr. Ahmed Hassan", AppRoles.Teacher);
        var teacher2 = await CreateUserAsync("sara.teacher@learnspace.dev", "Sara Mostafa", AppRoles.Teacher);
        var student1 = await CreateUserAsync("student1@learnspace.dev", "Omar Khaled", AppRoles.Student);
        var student2 = await CreateUserAsync("student2@learnspace.dev", "Laila Ibrahim", AppRoles.Student);
        var student3 = await CreateUserAsync("student3@learnspace.dev", "Youssef Adel", AppRoles.Student);

        db.TeacherProfiles.AddRange(
            new TeacherProfile
            {
                UserId = teacher1.Id,
                Bio = "Software engineer and lecturer. I teach programming from first principles.",
                AccentColor = "#0F766E"
            },
            new TeacherProfile
            {
                UserId = teacher2.Id,
                Bio = "Mathematics teacher with ten years of experience helping students love calculus.",
                AccentColor = "#7C3AED"
            });

        // ---------- courses: two Published, one Draft ----------
        var csharp = new Course
        {
            TeacherId = teacher1.Id,
            Title = "Introduction to C# Programming",
            Description = "Variables, control flow, classes and collections. No prior experience needed.",
            Subject = "Programming",
            Status = CourseStatus.Published
        };
        var aspnet = new Course
        {
            TeacherId = teacher1.Id,
            Title = "Advanced ASP.NET Core",
            Description = "Work in progress: middleware, authentication and performance.",
            Subject = "Programming",
            Status = CourseStatus.Draft
        };
        var calculus = new Course
        {
            TeacherId = teacher2.Id,
            Title = "Calculus I",
            Description = "Limits, derivatives and the basics of integration.",
            Subject = "Mathematics",
            Status = CourseStatus.Published
        };
        db.Courses.AddRange(csharp, aspnet, calculus);
        await db.SaveChangesAsync();   // courses now have their ids

        // ---------- one pre-made enrollment (student3 in the C# course) ----------
        db.Enrollments.Add(new Enrollment { CourseId = csharp.Id, StudentId = student3.Id, EnrolledAt = DateTime.UtcNow });

        await db.SaveChangesAsync();
    }

    /// <summary>Seeds the tutoring modules after the original LearnSpace MVP demo data.</summary>
    public async Task SeedTutoringAsync()
    {
        if (await db.Students.AnyAsync()) return;

        var teacher1 = await users.FindByEmailAsync("ahmed.teacher@learnspace.dev") ?? throw new InvalidOperationException("Run the base seed first.");
        var teacher2 = await users.FindByEmailAsync("sara.teacher@learnspace.dev") ?? throw new InvalidOperationException("Run the base seed first.");
        var student1 = await users.FindByEmailAsync("student1@learnspace.dev") ?? throw new InvalidOperationException("Run the base seed first.");
        var student2 = await users.FindByEmailAsync("student2@learnspace.dev") ?? throw new InvalidOperationException("Run the base seed first.");
        var student3 = await users.FindByEmailAsync("student3@learnspace.dev") ?? throw new InvalidOperationException("Run the base seed first.");
        var parentUser = await EnsureUserAsync("parent1@learnspace.dev", "Mona Khaled", AppRoles.Parent);
        var staffUser = await EnsureUserAsync("assistant@learnspace.dev", "Yara Assistant", AppRoles.Staff);
        _ = await EnsureUserAsync("admin@learnspace.dev", "Demo Admin", AppRoles.Admin);

        var s1 = new Student { UserId = student1.Id, Code = "STU-1001" };
        var s2 = new Student { UserId = student2.Id, Code = "STU-1002" };
        var s3 = new Student { UserId = student3.Id, Code = "STU-1003" };
        db.Students.AddRange(s1, s2, s3);
        var parent = new Parent { UserId = parentUser.Id };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        db.StudentParents.AddRange(
            new StudentParent { StudentId = s1.Id, ParentId = parent.Id, Relation = "Mother", IsPrimary = true },
            new StudentParent { StudentId = s2.Id, ParentId = parent.Id, Relation = "Mother", IsPrimary = true });
        db.Staff.Add(new Staff { UserId = staffUser.Id, TeacherId = teacher1.Id, JobTitle = "Teaching assistant" });

        var math = new Subject { Name = "Mathematics" };
        var programming = new Subject { Name = "Programming" };
        var secondary = new Level { Name = "Secondary 2" };
        var preparatory = new Level { Name = "Preparatory 3" };
        db.Subjects.AddRange(math, programming); db.Levels.AddRange(secondary, preparatory);
        await db.SaveChangesAsync();
        var mathLevel = new SubjectLevel { SubjectId = math.Id, LevelId = secondary.Id };
        var programmingLevel = new SubjectLevel { SubjectId = programming.Id, LevelId = preparatory.Id };
        db.SubjectLevels.AddRange(mathLevel, programmingLevel);
        await db.SaveChangesAsync();

        var monthly = new StudyGroup { TeacherId = teacher1.Id, SubjectLevelId = programmingLevel.Id, Name = "C# Foundations · Monthly", Capacity = 18, Price = 500, PriceType = PriceType.Monthly };
        var perSession = new StudyGroup { TeacherId = teacher2.Id, SubjectLevelId = mathLevel.Id, Name = "Algebra · Per session", Capacity = 12, Price = 300, PriceType = PriceType.Session };
        db.StudyGroups.AddRange(monthly, perSession);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstEnrollment = new GroupEnrollment { StudentId = s1.Id, GroupId = monthly.Id, StartDate = today };
        var secondEnrollment = new GroupEnrollment { StudentId = s2.Id, GroupId = monthly.Id, StartDate = today };
        var thirdEnrollment = new GroupEnrollment { StudentId = s3.Id, GroupId = monthly.Id, StartDate = today };
        var fourthEnrollment = new GroupEnrollment { StudentId = s1.Id, GroupId = perSession.Id, StartDate = today };
        db.GroupEnrollments.AddRange(firstEnrollment, secondEnrollment, thirdEnrollment, fourthEnrollment);
        await db.SaveChangesAsync();

        var sessionDate = today.AddDays((7 + (int)DayOfWeek.Wednesday - (int)today.DayOfWeek) % 7);
        var monthlySession = new ClassSession { GroupId = monthly.Id, Date = sessionDate, StartTime = new TimeOnly(17, 0), DurationMin = 90 };
        var mathSession = new ClassSession { GroupId = perSession.Id, Date = sessionDate, StartTime = new TimeOnly(19, 0), DurationMin = 75 };
        db.ClassSessions.AddRange(monthlySession, mathSession);
        db.GroupSchedules.AddRange(
            new GroupSchedule { GroupId = monthly.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(18, 30) },
            new GroupSchedule { GroupId = perSession.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(19, 0), EndTime = new TimeOnly(20, 15) });
        await db.SaveChangesAsync();
    }

    private async Task<ApplicationUser> EnsureUserAsync(string email, string displayName, string role)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, role)) await users.AddToRoleAsync(existing, role);
            return existing;
        }
        return await CreateUserAsync(email, displayName, role);
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string displayName, string role)
    {
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = displayName, EmailConfirmed = true };

        var created = await users.CreateAsync(user, DevPassword);
        if (!created.Succeeded)
            throw new InvalidOperationException($"Seeding {email} failed: " + string.Join(" ", created.Errors.Select(e => e.Description)));

        var inRole = await users.AddToRoleAsync(user, role);
        if (!inRole.Succeeded)
            throw new InvalidOperationException($"Seeding role for {email} failed: " + string.Join(" ", inRole.Errors.Select(e => e.Description)));

        return user;
    }
}
