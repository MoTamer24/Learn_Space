using System.Text;
using LearnSpace.Api.Common.Storage;
using LearnSpace.Api.Modules.Assessments;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Content;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Data.Seed;

/// <summary>Development-only demo data. Runs once: it does nothing if any user already exists.</summary>
public class DbSeeder(AppDbContext db, UserManager<ApplicationUser> users, IStorageService storage)
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

        // ---------- materials with tiny placeholder files ----------
        db.Materials.AddRange(
            new Material
            {
                CourseId = csharp.Id, Title = "Welcome video", Type = MaterialType.Video, Position = 1,
                StorageKey = await SavePlaceholderAsync("welcome.mp4", "placeholder video bytes"),
                FileName = "welcome.mp4", ContentType = "video/mp4"
            },
            new Material
            {
                CourseId = csharp.Id, Title = "Course syllabus", Type = MaterialType.Document, Position = 2,
                StorageKey = await SavePlaceholderAsync("syllabus.pdf", "placeholder pdf bytes"),
                FileName = "syllabus.pdf", ContentType = "application/pdf"
            },
            new Material
            {
                CourseId = calculus.Id, Title = "Limits cheat sheet", Type = MaterialType.Document, Position = 1,
                StorageKey = await SavePlaceholderAsync("limits.pdf", "placeholder pdf bytes"),
                FileName = "limits.pdf", ContentType = "application/pdf"
            });

        // ---------- one published quiz: 2 MCQ + 1 written ----------
        db.Quizzes.Add(new Quiz
        {
            CourseId = csharp.Id,
            Title = "C# Basics Quiz",
            DurationMinutes = 20,
            Status = QuizStatus.Published,
            Questions =
            [
                new Question
                {
                    Type = QuestionType.Mcq, Text = "Which keyword declares a compile-time constant?", Points = 2, Position = 1,
                    Options =
                    [
                        new QuestionOption { Text = "const", IsCorrect = true },
                        new QuestionOption { Text = "static" },
                        new QuestionOption { Text = "readonly" },
                        new QuestionOption { Text = "final" }
                    ]
                },
                new Question
                {
                    Type = QuestionType.Mcq, Text = "What is the default value of an int field?", Points = 2, Position = 2,
                    Options =
                    [
                        new QuestionOption { Text = "0", IsCorrect = true },
                        new QuestionOption { Text = "null" },
                        new QuestionOption { Text = "-1" }
                    ]
                },
                new Question
                {
                    Type = QuestionType.Written, Text = "In your own words, explain the difference between a class and an object.", Points = 6, Position = 3
                }
            ]
        });

        // ---------- two tasks: one still open, one whose deadline already passed ----------
        db.CourseTasks.AddRange(
            new CourseTask
            {
                CourseId = csharp.Id,
                Title = "Build a console calculator",
                Instructions = "Write a small console calculator in C# and upload your project as a PDF report.",
                DeadlineAt = DateTime.UtcNow.AddDays(7),
                MaxScore = 20
            },
            new CourseTask
            {
                CourseId = csharp.Id,
                Title = "Hello World exercise (closed)",
                Instructions = "Print Hello World and explain each line. The deadline has already passed.",
                DeadlineAt = DateTime.UtcNow.AddDays(-2),
                MaxScore = 10
            });

        await db.SaveChangesAsync();
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

    private async Task<string> SavePlaceholderAsync(string fileName, string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return await storage.SaveAsync(stream, fileName);
    }
}
