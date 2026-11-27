using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Enrollments;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Groups;
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

    // Enrollment
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Enums are stored as strings.
        configurationBuilder.Properties<CourseStatus>().HaveConversion<string>();
        configurationBuilder.Properties<GroupStatus>().HaveConversion<string>();
        configurationBuilder.Properties<PriceType>().HaveConversion<string>();
        configurationBuilder.Properties<EnrollmentStatus>().HaveConversion<string>();
        configurationBuilder.Properties<ClassSessionStatus>().HaveConversion<string>();
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
        b.Entity<GroupEnrollment>(e => { e.HasIndex(x => new { x.StudentId, x.GroupId }).IsUnique().HasFilter("\"Status\" = 'Active'"); e.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict); e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<ClassSession>(e => e.HasOne<StudyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict));

        b.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.CourseId, x.StudentId }).IsUnique();
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
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
