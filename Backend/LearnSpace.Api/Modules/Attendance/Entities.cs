namespace LearnSpace.Api.Modules.Attendance;

public enum AttendanceStatus { Present, Absent, Late }
public class Attendance
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid SessionId { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public record CheckInResult(bool Allowed, string Message, Guid? AttendanceId, decimal EnrollmentBalance, decimal StudentBalance, LatestPaymentInfo? LatestPayment);
public record LatestPaymentInfo(decimal AmountPaid, DateTime PaymentDate, string? BillingMonth, Guid? SessionId);
