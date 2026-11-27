using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Catalog;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Groups;

public class GroupService(AppDbContext db, ITeacherService teachers, ICatalogLookup catalog, IStudentDirectory students) : IGroupDirectory
{
    public Task<List<StudyGroup>> GetGroupsAsync(Guid? teacherId = null, CancellationToken ct = default)
    { var query = db.StudyGroups.AsNoTracking(); if (teacherId.HasValue) query = query.Where(x => x.TeacherId == teacherId); return query.OrderBy(x => x.Name).ToListAsync(ct); }

    public async Task<StudyGroup> CreateGroupAsync(CreateStudyGroupRequest request, CancellationToken ct = default)
    {
        if (request.Capacity <= 0 || request.Price < 0) throw new InvalidOperationException("Capacity must be positive and price cannot be negative.");
        if (!await teachers.ExistsAsync(request.TeacherId)) throw new InvalidOperationException("Teacher not found.");
        if (!await catalog.SubjectLevelExistsAsync(request.SubjectLevelId)) throw new InvalidOperationException("Subject level not found.");
        var group = new StudyGroup { TeacherId = request.TeacherId, SubjectLevelId = request.SubjectLevelId, Name = request.Name.Trim(), Capacity = request.Capacity, Price = request.Price, PriceType = request.PriceType };
        db.StudyGroups.Add(group); await db.SaveChangesAsync(ct); return group;
    }

    public async Task<GroupEnrollment> EnrollAsync(Guid groupId, CreateEnrollmentRequest request, CancellationToken ct = default)
    {
        var group = await db.StudyGroups.SingleOrDefaultAsync(x => x.Id == groupId, ct) ?? throw new KeyNotFoundException("Group not found.");
        if (!await students.StudentExistsAsync(request.StudentId, ct)) throw new InvalidOperationException("Student not found.");
        if (await db.GroupEnrollments.AnyAsync(x => x.GroupId == groupId && x.StudentId == request.StudentId && x.Status == EnrollmentStatus.Active, ct)) throw new InvalidOperationException("Student is already actively enrolled.");
        var count = await db.GroupEnrollments.CountAsync(x => x.GroupId == groupId && x.Status == EnrollmentStatus.Active, ct);
        if (count >= group.Capacity) throw new InvalidOperationException("Group capacity has been reached.");
        var enrollment = new GroupEnrollment { GroupId = groupId, StudentId = request.StudentId, StartDate = request.StartDate };
        db.GroupEnrollments.Add(enrollment); await db.SaveChangesAsync(ct);
        return enrollment;
    }

    public async Task<GroupSchedule> AddScheduleAsync(Guid groupId, CreateScheduleRequest request, CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime) throw new InvalidOperationException("End time must follow start time.");
        if (!await db.StudyGroups.AnyAsync(x => x.Id == groupId, ct)) throw new KeyNotFoundException("Group not found.");
        var schedule = new GroupSchedule { GroupId = groupId, DayOfWeek = request.DayOfWeek, StartTime = request.StartTime, EndTime = request.EndTime };
        db.GroupSchedules.Add(schedule); await db.SaveChangesAsync(ct); return schedule;
    }

    public async Task<ClassSession> AddSessionAsync(Guid groupId, CreateSessionRequest request, CancellationToken ct = default)
    {
        if (request.DurationMin <= 0) throw new InvalidOperationException("Duration must be positive.");
        if (!await db.StudyGroups.AnyAsync(x => x.Id == groupId, ct)) throw new KeyNotFoundException("Group not found.");
        var session = new ClassSession { GroupId = groupId, Date = request.Date, StartTime = request.StartTime, DurationMin = request.DurationMin };
        db.ClassSessions.Add(session); await db.SaveChangesAsync(ct); return session;
    }

    public Task<List<GroupEnrollment>> GetEnrollmentsAsync(Guid groupId, CancellationToken ct = default) => db.GroupEnrollments.AsNoTracking().Where(x => x.GroupId == groupId).ToListAsync(ct);
    public Task<List<ClassSession>> GetSessionsAsync(Guid groupId, CancellationToken ct = default) => db.ClassSessions.AsNoTracking().Where(x => x.GroupId == groupId).OrderByDescending(x => x.Date).ToListAsync(ct);
    public Task<List<GroupSchedule>> GetSchedulesAsync(Guid groupId, CancellationToken ct = default) => db.GroupSchedules.AsNoTracking().Where(x => x.GroupId == groupId).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).ToListAsync(ct);
    public Task<bool> IsStudentActiveInGroupAsync(Guid studentId, Guid groupId, CancellationToken ct = default) => db.GroupEnrollments.AnyAsync(x => x.StudentId == studentId && x.GroupId == groupId && x.Status == EnrollmentStatus.Active, ct);
    public Task<bool> IsGroupOwnedByTeacherAsync(Guid teacherId, Guid groupId, CancellationToken ct = default) => db.StudyGroups.AnyAsync(x => x.Id == groupId && x.TeacherId == teacherId, ct);
    public Task<bool> IsSessionInGroupAsync(Guid sessionId, Guid groupId, CancellationToken ct = default) => db.ClassSessions.AnyAsync(x => x.Id == sessionId && x.GroupId == groupId, ct);
    public Task<bool> IsSessionOwnedByTeacherAsync(Guid teacherId, Guid sessionId, CancellationToken ct = default) => db.ClassSessions.AnyAsync(s => s.Id == sessionId && db.StudyGroups.Any(g => g.Id == s.GroupId && g.TeacherId == teacherId), ct);
    public Task<List<Guid>> GetActiveGroupIdsForStudentAsync(Guid studentId, CancellationToken ct = default) => db.GroupEnrollments.AsNoTracking().Where(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Active).Select(x => x.GroupId).Distinct().ToListAsync(ct);
    public Task<List<Guid>> GetActiveStudentIdsForTeacherAsync(Guid teacherId, CancellationToken ct = default) =>
        (from enrollment in db.GroupEnrollments.AsNoTracking()
         join studyGroup in db.StudyGroups.AsNoTracking() on enrollment.GroupId equals studyGroup.Id
         where studyGroup.TeacherId == teacherId && enrollment.Status == EnrollmentStatus.Active
         select enrollment.StudentId).Distinct().ToListAsync(ct);
}
