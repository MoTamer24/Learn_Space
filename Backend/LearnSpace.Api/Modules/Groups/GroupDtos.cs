using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Groups;

public record CreateStudyGroupRequest(Guid TeacherId, Guid SubjectLevelId,
    [property: Required, StringLength(160)] string Name, int Capacity, decimal Price, PriceType PriceType);
public record CreateScheduleRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public record CreateEnrollmentRequest(Guid StudentId, DateOnly StartDate);
public record CreateSessionRequest(DateOnly Date, TimeOnly StartTime, int DurationMin);
