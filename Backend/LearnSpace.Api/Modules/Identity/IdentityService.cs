using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Identity;

public class IdentityService(AppDbContext db, UserManager<ApplicationUser> users, ITeacherService teachers) : IStudentDebtService, IStaffDirectory
{
    public async Task<ApplicationUser> CreateUserAsync(CreateIdentityUserRequest request, CancellationToken ct = default)
    {
        var role = AppRoles.All.FirstOrDefault(x => string.Equals(x, request.Role, StringComparison.OrdinalIgnoreCase));
        if (role is null) throw new InvalidOperationException("Role must be Admin, Teacher, Student, Parent, or Staff.");
        if (!string.IsNullOrWhiteSpace(request.Phone) && await db.Users.AnyAsync(x => x.PhoneNumber == request.Phone.Trim(), ct))
            throw new InvalidOperationException("Phone number is already in use.");
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(), Email = request.Email.Trim(), PhoneNumber = request.Phone,
            DisplayName = request.Name.Trim(), EmailConfirmed = true, IsActive = true
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join(" ", created.Errors.Select(x => x.Description)));
        var assigned = await users.AddToRoleAsync(user, role);
        if (!assigned.Succeeded) throw new InvalidOperationException(string.Join(" ", assigned.Errors.Select(x => x.Description)));
        if (role == AppRoles.Teacher) await teachers.CreateEmptyProfileAsync(user.Id);
        return user;
    }

    public async Task<Student> CreateStudentAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString()) ?? throw new KeyNotFoundException("User not found.");
        if (!await users.IsInRoleAsync(user, AppRoles.Student)) throw new InvalidOperationException("The user must have the Student role.");
        if (await db.Students.AnyAsync(x => x.UserId == request.UserId || (request.Code != null && x.Code == request.Code), ct))
            throw new InvalidOperationException("A student profile or code already exists.");
        var student = new Student { UserId = request.UserId, Code = string.IsNullOrWhiteSpace(request.Code) ? Guid.NewGuid().ToString("N")[..10].ToUpperInvariant() : request.Code.Trim(), MaxDebt = request.MaxDebt };
        db.Students.Add(student);
        await db.SaveChangesAsync(ct);
        return student;
    }

    public Task<List<StudentProfileDto>> GetStudentsAsync(IReadOnlyCollection<Guid>? studentIds = null, CancellationToken ct = default)
    {
        var query = from s in db.Students.AsNoTracking()
         join u in db.Users.AsNoTracking() on s.UserId equals u.Id
         where !s.IsDeleted
         orderby u.DisplayName
         select new StudentProfileDto(s.Id, s.UserId, u.DisplayName, s.Code, s.OutstandingBalance, s.MaxDebt);
        if (studentIds is not null) query = query.Where(x => studentIds.Contains(x.Id));
        return query.ToListAsync(ct);
    }

    public async Task<StudentDebtSnapshot?> GetDebtSnapshotAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.AsNoTracking().SingleOrDefaultAsync(x => x.Id == studentId && !x.IsDeleted, cancellationToken);
        if (student is null) return null;
        var parentUserIds = await (from link in db.StudentParents.AsNoTracking()
                                   join parent in db.Parents.AsNoTracking() on link.ParentId equals parent.Id
                                   where link.StudentId == studentId
                                   select parent.UserId).Distinct().ToListAsync(cancellationToken);
        return new StudentDebtSnapshot(student.Id, student.OutstandingBalance, student.MaxDebt, parentUserIds);
    }

    public Task<Guid?> GetStudentIdForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Students.Where(x => x.UserId == userId && !x.IsDeleted).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<List<Guid>> GetStudentIdsForParentUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (from parent in db.Parents.AsNoTracking()
         join link in db.StudentParents.AsNoTracking() on parent.Id equals link.ParentId
         where parent.UserId == userId
         select link.StudentId).Distinct().ToListAsync(cancellationToken);

    public Task<Guid?> GetTeacherIdForStaffUserAsync(Guid staffUserId, CancellationToken cancellationToken = default) =>
        db.Staff.Where(x => x.UserId == staffUserId).Select(x => (Guid?)x.TeacherId).SingleOrDefaultAsync(cancellationToken);

    public async Task ApplyEnrollmentBalanceChangeAsync(Guid studentId, decimal delta, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.SingleOrDefaultAsync(x => x.Id == studentId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Student profile not found.");
        student.OutstandingBalance = Math.Max(0, student.OutstandingBalance + delta);
        student.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
