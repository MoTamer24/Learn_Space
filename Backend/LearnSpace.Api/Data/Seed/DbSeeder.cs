using System.Text;
using LearnSpace.Api.Common.Storage;
using LearnSpace.Api.Modules.Assessments;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Content;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Groups;
using LearnSpace.Api.Modules.Attendance;
using LearnSpace.Api.Modules.Payments;
using LearnSpace.Api.Modules.Notifications;
using LearnSpace.Api.Modules.Subscriptions;
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

        var s1 = new Student { UserId = student1.Id, Code = "STU-1001", MaxDebt = 1000 };
        var s2 = new Student { UserId = student2.Id, Code = "STU-1002", MaxDebt = 1500 };
        var s3 = new Student { UserId = student3.Id, Code = "STU-1003", MaxDebt = 800 };
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
        var currentMonth = today.ToString("yyyy-MM");
        var monthlyEnrollment = new GroupEnrollment { StudentId = s1.Id, GroupId = monthly.Id, StartDate = today, OutstandingBalance = 200, MaxDebt = 500 };
        var overLimitEnrollment = new GroupEnrollment { StudentId = s2.Id, GroupId = monthly.Id, StartDate = today, OutstandingBalance = 500, MaxDebt = 400 };
        var exemptEnrollment = new GroupEnrollment { StudentId = s3.Id, GroupId = monthly.Id, StartDate = today, ExemptionType = ExemptionType.Full, ExemptionReason = "Scholarship", OutstandingBalance = 0 };
        var sessionEnrollment = new GroupEnrollment { StudentId = s1.Id, GroupId = perSession.Id, StartDate = today, OutstandingBalance = 300, MaxDebt = 600 };
        db.GroupEnrollments.AddRange(monthlyEnrollment, overLimitEnrollment, exemptEnrollment, sessionEnrollment);
        await db.SaveChangesAsync();
        db.Students.Where(x => x.Id == s1.Id).ExecuteUpdate(x => x.SetProperty(p => p.OutstandingBalance, 500m));
        db.Students.Where(x => x.Id == s2.Id).ExecuteUpdate(x => x.SetProperty(p => p.OutstandingBalance, 500m));

        var sessionDate = today.AddDays((7 + (int)DayOfWeek.Wednesday - (int)today.DayOfWeek) % 7);
        var monthlySession = new ClassSession { GroupId = monthly.Id, Date = sessionDate, StartTime = new TimeOnly(17, 0), DurationMin = 90 };
        var mathSession = new ClassSession { GroupId = perSession.Id, Date = sessionDate, StartTime = new TimeOnly(19, 0), DurationMin = 75 };
        db.ClassSessions.AddRange(monthlySession, mathSession);
        db.GroupSchedules.AddRange(
            new GroupSchedule { GroupId = monthly.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(18, 30) },
            new GroupSchedule { GroupId = perSession.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(19, 0), EndTime = new TimeOnly(20, 15) });
        db.Payments.Add(new Payment { GroupEnrollmentId = monthlyEnrollment.Id, AmountPaid = 300, BillingMonth = currentMonth, Method = PaymentMethod.VodafoneCash });
        await db.SaveChangesAsync();

        var assessment = new Assessment { TeacherId = teacher1.Id, GroupId = monthly.Id, SessionId = monthlySession.Id, Title = "C# basics check", Type = AssessmentType.Quiz, Date = sessionDate.ToDateTime(new TimeOnly(17, 0)), Duration = 20, TotalMarks = 10 };
        var bankQuestion = new QuestionBankItem { TeacherId = teacher1.Id, SubjectLevelId = programmingLevel.Id, QuestionText = "Which keyword declares a compile-time constant in C#?", Type = TutoringQuestionType.MCQ };
        db.TutoringAssessments.Add(assessment);
        db.QuestionBankItems.Add(bankQuestion);
        db.QuestionBankOptions.AddRange(
            new QuestionBankOption { QuestionId = bankQuestion.Id, OptionText = "const", IsCorrect = true },
            new QuestionBankOption { QuestionId = bankQuestion.Id, OptionText = "readonly" });
        db.AssessmentQuestions.Add(new AssessmentQuestion { AssessmentId = assessment.Id, QuestionId = bankQuestion.Id, QuestionOrder = 1, Mark = 10 });
        db.TutoringMaterials.Add(new TutoringMaterial { TeacherId = teacher1.Id, GroupId = monthly.Id, SessionId = monthlySession.Id, Title = "Variables and types notes", Type = TutoringMaterialType.Notes, Url = "https://example.com/csharp-notes", SizeMB = 0.2m });
        var plan = new Plan { Name = "Solo Teacher", Price = 299, MaxStudents = 100, MaxGroups = 10, MaxStorageMB = 2048 };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        var subscription = new TeacherSubscription { TeacherId = teacher1.Id, PlanId = plan.Id, StartDate = today, EndDate = today.AddMonths(1), Status = SubscriptionStatus.Active, StorageUsedMB = 0.2m };
        db.TeacherSubscriptions.Add(subscription);
        db.Notifications.Add(new Notification { TeacherId = teacher1.Id, UserId = parentUser.Id, Type = NotificationType.Session, Message = "Welcome to the C# Foundations group." });
        db.TeacherPayments.Add(new TeacherPayment { TeacherSubscriptionId = subscription.Id, BillingMonth = currentMonth, Amount = plan.Price, Status = TeacherPaymentStatus.Paid });
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

    private async Task<string> SavePlaceholderAsync(string fileName, string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return await storage.SaveAsync(stream, fileName);
    }
}
