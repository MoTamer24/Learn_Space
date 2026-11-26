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
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<SubjectLevel> SubjectLevels => Set<SubjectLevel>();

    // Identity profiles
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<StudentParent> StudentParents => Set<StudentParent>();
    public DbSet<Staff> Staff => Set<Staff>();

    // Groups
    public DbSet<StudyGroup> StudyGroups => Set<StudyGroup>();
    public DbSet<GroupSchedule> GroupSchedules => Set<GroupSchedule>();
    public DbSet<GroupEnrollment> GroupEnrollments => Set<GroupEnrollment>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();

    // Attendance and payments
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Payment> Payments => Set<Payment>();

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
    public DbSet<QuestionBankItem> QuestionBankItems => Set<QuestionBankItem>();
    public DbSet<QuestionBankOption> QuestionBankOptions => Set<QuestionBankOption>();
    public DbSet<Assessment> TutoringAssessments => Set<Assessment>();
    public DbSet<AssessmentQuestion> AssessmentQuestions => Set<AssessmentQuestion>();
    public DbSet<StudentAssessment> StudentAssessments => Set<StudentAssessment>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();

    // Content, notifications, subscriptions
    public DbSet<TutoringMaterial> TutoringMaterials => Set<TutoringMaterial>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<TeacherSubscription> TeacherSubscriptions => Set<TeacherSubscription>();
    public DbSet<TeacherPayment> TeacherPayments => Set<TeacherPayment>();

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
        configurationBuilder.Properties<GroupStatus>().HaveConversion<string>();
        configurationBuilder.Properties<PriceType>().HaveConversion<string>();
        configurationBuilder.Properties<EnrollmentStatus>().HaveConversion<string>();
        configurationBuilder.Properties<ExemptionType>().HaveConversion<string>();
        configurationBuilder.Properties<ClassSessionStatus>().HaveConversion<string>();
        configurationBuilder.Properties<AttendanceStatus>().HaveConversion<string>();
        configurationBuilder.Properties<PaymentMethod>().HaveConversion<string>();
        configurationBuilder.Properties<TutoringQuestionType>().HaveConversion<string>();
        configurationBuilder.Properties<AssessmentType>().HaveConversion<string>();
        configurationBuilder.Properties<TutoringMaterialType>().HaveConversion<string>();
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>();
        configurationBuilder.Properties<NotificationChannel>().HaveConversion<string>();
        configurationBuilder.Properties<NotificationStatus>().HaveConversion<string>();
        configurationBuilder.Properties<SubscriptionStatus>().HaveConversion<string>();
        configurationBuilder.Properties<TeacherPaymentStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Every foreign key uses Restrict so grades and history are never cascaded away.

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.HasIndex(u => u.NormalizedEmail).IsUnique();   // spec: Email is unique (also enforced by Identity options)
            e.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL");
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

        b.Entity<Subject>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Level>().HasIndex(x => x.Name).IsUnique();
        b.Entity<SubjectLevel>(e => { e.HasIndex(x => new { x.SubjectId, x.LevelId }).IsUnique(); e.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Level>().WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict); });

        b.Entity<Student>(e => { e.HasIndex(x => x.UserId).IsUnique(); e.HasIndex(x => x.Code).IsUnique(); e.HasQueryFilter(x => !x.IsDeleted); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Parent>(e => { e.HasIndex(x => x.UserId).IsUnique(); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<StudentParent>(e => { e.HasKey(x => new { x.StudentId, x.ParentId }); e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Parent>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Staff>(e => { e.HasIndex(x => x.UserId).IsUnique(); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); e.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict); });

        b.Entity<StudyGroup>(e => { e.Property(x => x.Price).HasPrecision(10, 2); e.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict); e.HasOne<SubjectLevel>().WithMany().HasForeignKey(x => x.SubjectLevelId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<GroupSchedule>(e => e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict));
        b.Entity<GroupEnrollment>(e => { e.Property(x => x.Discount).HasPrecision(10, 2); e.Property(x => x.OutstandingBalance).HasPrecision(10, 2); e.Property(x => x.MaxDebt).HasPrecision(10, 2); e.HasIndex(x => new { x.StudentId, x.GroupId }).IsUnique().HasFilter("\"Status\" = 'Active'"); e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<ClassSession>(e => e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict));
        b.Entity<Attendance>(e => { e.HasIndex(x => new { x.StudentId, x.SessionId }).IsUnique(); e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Payment>(e => { e.Property(x => x.AmountPaid).HasPrecision(10, 2); e.HasQueryFilter(x => !x.IsDeleted); e.HasOne<GroupEnrollment>().WithMany().HasForeignKey(x => x.GroupEnrollmentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); });

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

        b.Entity<QuestionBankItem>(e => { e.Property(x => x.QuestionText).HasMaxLength(4000); e.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict); e.HasOne<SubjectLevel>().WithMany().HasForeignKey(x => x.SubjectLevelId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<QuestionBankOption>(e => e.HasOne<QuestionBankItem>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict));
        b.Entity<Assessment>(e => { e.Property(x => x.TotalMarks).HasPrecision(10, 2); e.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict); e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<AssessmentQuestion>(e => { e.HasKey(x => new { x.AssessmentId, x.QuestionId }); e.HasIndex(x => new { x.AssessmentId, x.QuestionOrder }).IsUnique(); e.Property(x => x.Mark).HasPrecision(10, 2); e.HasOne<Assessment>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<QuestionBankItem>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<StudentAssessment>(e => { e.HasIndex(x => new { x.StudentId, x.AssessmentId }).IsUnique(); e.Property(x => x.Mark).HasPrecision(10, 2); e.HasOne<Assessment>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<StudentAnswer>(e => { e.Property(x => x.MarkAwarded).HasPrecision(10, 2); e.HasIndex(x => new { x.StudentAssessmentId, x.QuestionId }).IsUnique(); e.HasOne<StudentAssessment>().WithMany().HasForeignKey(x => x.StudentAssessmentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<QuestionBankItem>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict); e.HasOne<QuestionBankOption>().WithMany().HasForeignKey(x => x.SelectedOptionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TutoringMaterial>(e => { e.Property(x => x.SizeMB).HasPrecision(10, 2); e.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict); e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict); e.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Notification>(e => { e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); e.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Plan>(e => e.Property(x => x.Price).HasPrecision(10, 2));
        b.Entity<TeacherSubscription>(e => { e.Property(x => x.StorageUsedMB).HasPrecision(10, 2); e.HasOne<TeacherProfile>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<TeacherPayment>(e => { e.Property(x => x.Amount).HasPrecision(10, 2); e.HasOne<TeacherSubscription>().WithMany().HasForeignKey(x => x.TeacherSubscriptionId).OnDelete(DeleteBehavior.Restrict); });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampUpdatedAt(); return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampUpdatedAt(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    private void StampUpdatedAt()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Modified && x.Metadata.FindProperty("UpdatedAt") is not null))
            entry.Property("UpdatedAt").CurrentValue = now;
    }
}
