using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Groups;

public record CreateStudyGroupRequest(Guid TeacherId, Guid SubjectLevelId,
    [property: Required, StringLength(160)] string Name, int Capacity, decimal Price, PriceType PriceType);
public record CreateScheduleRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public record CreateEnrollmentRequest(Guid StudentId, DateOnly StartDate, ExemptionType ExemptionType,
    string? ExemptionReason, decimal Discount, decimal? MaxDebt);
public record CreateSessionRequest(DateOnly Date, TimeOnly StartTime, int DurationMin);
public record AttendanceCheckInRequest(Guid StudentId, Guid SessionId, LearnSpace.Api.Modules.Attendance.AttendanceStatus Status);
