using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Groups;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Notifications;
using LearnSpace.Api.Modules.Payments;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Attendance;

public class AttendanceService(AppDbContext db, IGroupBillingService groups, IStudentDebtService students,
    IPaymentService payments, INotificationService notifications)
{
    public Task<List<Attendance>> GetAsync(Guid? sessionId, CancellationToken ct = default)
    { var query = db.Attendances.AsNoTracking(); if (sessionId.HasValue) query = query.Where(x => x.SessionId == sessionId); return query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct); }

    public async Task<CheckInResult> CheckInAsync(Guid studentId, Guid sessionId, AttendanceStatus status, CancellationToken ct = default)
    {
        var group = await groups.GetCheckInSnapshotAsync(studentId, sessionId, ct);
        if (group is null) return new CheckInResult(false, "Student is not actively enrolled in this session's group.", null, 0, 0, null);

        // PriceType determines which payment period/key is consulted.
        var billingMonth = $"{group.SessionDate.Year:D4}-{group.SessionDate.Month:D2}";
        var latest = await payments.GetLatestAsync(group.EnrollmentId, group.PriceType, group.SessionId, billingMonth, ct);
        var enrollmentBalance = await payments.RefreshOutstandingAsync(group.EnrollmentId, group.PriceType, group.SessionId, billingMonth, ct);
        var student = await students.GetDebtSnapshotAsync(studentId, ct);
        if (student is null) return new CheckInResult(false, "Student profile was not found.", null, enrollmentBalance, 0, null);
        var latestInfo = latest is null ? null : new LatestPaymentInfo(latest.AmountPaid, latest.PaymentDate!.Value, latest.BillingMonth, latest.SessionId);
        var enrollmentOverLimit = group.EnrollmentMaxDebt is not null && enrollmentBalance > group.EnrollmentMaxDebt;
        var studentOverLimit = student.MaxDebt is not null && student.OutstandingBalance > student.MaxDebt;
        if (group.ExemptionType != ExemptionType.Full && (enrollmentOverLimit || studentOverLimit))
        {
            var reason = enrollmentOverLimit ? "group debt limit" : "student debt limit";
            var message = $"Check-in blocked for student {studentId}: outstanding balance exceeds the {reason}.";
            await notifications.NotifyAsync(group.TeacherId, group.TeacherId, NotificationType.LatePayment, message, ct);
            await notifications.NotifyManyAsync(group.TeacherId, student.ParentUserIds, NotificationType.LatePayment, message, ct);
            return new CheckInResult(false, message, null, enrollmentBalance, student.OutstandingBalance, latestInfo);
        }

        if (await db.Attendances.AnyAsync(x => x.SessionId == sessionId && x.StudentId == studentId, ct))
            return new CheckInResult(false, "Attendance has already been recorded for this student and session.", null, enrollmentBalance, student.OutstandingBalance, latestInfo);
        var attendance = new Attendance { StudentId = studentId, SessionId = sessionId, Status = status };
        db.Attendances.Add(attendance); await db.SaveChangesAsync(ct);
        if (status == AttendanceStatus.Absent)
        {
            var absence = $"Student {studentId} was marked absent for session {sessionId}.";
            await notifications.NotifyAsync(group.TeacherId, group.TeacherId, NotificationType.Absence, absence, ct);
            await notifications.NotifyManyAsync(group.TeacherId, student.ParentUserIds, NotificationType.Absence, absence, ct);
        }
        return new CheckInResult(true, "Check-in accepted.", attendance.Id, enrollmentBalance, student.OutstandingBalance, latestInfo);
    }
}
