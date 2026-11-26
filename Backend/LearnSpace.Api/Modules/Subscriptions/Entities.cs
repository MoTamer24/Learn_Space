namespace LearnSpace.Api.Modules.Subscriptions;

public enum SubscriptionStatus { Active, Expired, Cancelled }
public enum TeacherPaymentStatus { Pending, Paid, Failed }
public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int MaxStudents { get; set; }
    public int MaxGroups { get; set; }
    public decimal MaxStorageMB { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class TeacherSubscription
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid PlanId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public decimal StorageUsedMB { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class TeacherPayment
{
    public Guid Id { get; set; }
    public Guid TeacherSubscriptionId { get; set; }
    public string BillingMonth { get; set; } = "";
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public TeacherPaymentStatus Status { get; set; } = TeacherPaymentStatus.Paid;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
