using LearnSpace.Api.Modules.Assessments;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Content;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Data;

/// <summary>One shared context that holds every module's DbSet. Each module only queries its own sets.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // Catalog
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<Course> Courses => Set<Course>();

    // Enrollment
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    // Content
    public DbSet<Material> Materials => Set<Material>();

    // Assessments
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
    public DbSet<CourseTask> CourseTasks => Set<CourseTask>();
    public DbSet<TaskSubmission> TaskSubmissions => Set<TaskSubmission>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Enums are stored as strings.
        configurationBuilder.Properties<CourseStatus>().HaveConversion<string>();
        configurationBuilder.Properties<MaterialType>().HaveConversion<string>();
        configurationBuilder.Properties<QuizStatus>().HaveConversion<string>();
        configurationBuilder.Properties<QuestionType>().HaveConversion<string>();
        configurationBuilder.Properties<AttemptStatus>().HaveConversion<string>();
        configurationBuilder.Properties<SubmissionStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Every foreign key uses Restrict so grades and history are never cascaded away.

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.HasIndex(u => u.NormalizedEmail).IsUnique();   // spec: Email is unique (also enforced by Identity options)
        });

        b.Entity<TeacherProfile>(e =>
        {
            e.HasKey(p => p.UserId);
            e.Property(p => p.UserId).ValueGeneratedNever();
            e.Property(p => p.Bio).HasMaxLength(2000);
            e.Property(p => p.LogoKey).HasMaxLength(100);
            e.Property(p => p.AccentColor).HasMaxLength(7);
            e.HasOne<ApplicationUser>().WithOne().HasForeignKey<TeacherProfile>(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Course>(e =>
        {
            e.Property(c => c.Title).HasMaxLength(200);
            e.Property(c => c.Description).HasMaxLength(5000);
            e.Property(c => c.Subject).HasMaxLength(100);
            e.HasIndex(c => c.Status);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.CourseId, x.StudentId }).IsUnique();
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Material>(e =>
        {
            e.Property(m => m.Title).HasMaxLength(200);
            e.Property(m => m.StorageKey).HasMaxLength(100);
            e.Property(m => m.FileName).HasMaxLength(255);
            e.Property(m => m.ContentType).HasMaxLength(150);
            e.HasIndex(m => m.CourseId);
            e.HasOne<Course>().WithMany().HasForeignKey(m => m.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Quiz>(e =>
        {
            e.Property(q => q.Title).HasMaxLength(200);
            e.HasOne<Course>().WithMany().HasForeignKey(q => q.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(q => q.Questions).WithOne().HasForeignKey(q => q.QuizId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Question>(e =>
        {
            e.Property(q => q.Text).HasMaxLength(2000);
            e.HasMany(q => q.Options).WithOne().HasForeignKey(o => o.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<QuestionOption>(e =>
        {
            e.Property(o => o.Text).HasMaxLength(500);
        });

        b.Entity<QuizAttempt>(e =>
        {
            e.Property(a => a.Score).HasPrecision(10, 2);
            e.HasIndex(a => new { a.QuizId, a.StudentId }).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Quiz>().WithMany().HasForeignKey(a => a.QuizId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(a => a.Answers).WithOne().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AttemptAnswer>(e =>
        {
            e.Property(a => a.TextAnswer).HasMaxLength(10000);
            e.Property(a => a.TeacherNote).HasMaxLength(2000);
            e.Property(a => a.AwardedPoints).HasPrecision(10, 2);
            e.HasIndex(a => new { a.AttemptId, a.QuestionId }).IsUnique();
            e.HasOne<Question>().WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<QuestionOption>().WithMany().HasForeignKey(a => a.SelectedOptionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CourseTask>(e =>
        {
            e.Property(t => t.Title).HasMaxLength(200);
            e.Property(t => t.Instructions).HasMaxLength(5000);
            e.Property(t => t.MaxScore).HasPrecision(10, 2);
            e.HasOne<Course>().WithMany().HasForeignKey(t => t.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaskSubmission>(e =>
        {
            e.Property(s => s.StorageKey).HasMaxLength(100);
            e.Property(s => s.FileName).HasMaxLength(255);
            e.Property(s => s.ContentType).HasMaxLength(150);
            e.Property(s => s.Score).HasPrecision(10, 2);
            e.Property(s => s.TeacherNote).HasMaxLength(2000);
            e.HasIndex(s => new { s.TaskId, s.StudentId }).IsUnique();
            e.HasOne<CourseTask>().WithMany().HasForeignKey(s => s.TaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
