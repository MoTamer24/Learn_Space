using LearnSpace.Api.Modules.Groups;

namespace LearnSpace.Api.Modules.Payments;

public enum PaymentMethod { Cash, VodafoneCash, InstaPay }

public class Payment
{
    public Guid Id { get; set; }
    public Guid GroupEnrollmentId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal AmountPaid { get; set; }
    public string? BillingMonth { get; set; }
    public Guid? SessionId { get; set; }
    public PaymentMethod Method { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public record LatestPaymentSnapshot(Guid? PaymentId, decimal AmountPaid, DateTime? PaymentDate, string? BillingMonth, Guid? SessionId);

public interface IPaymentService
{
    Task<LatestPaymentSnapshot?> GetLatestAsync(Guid enrollmentId, PriceType priceType, Guid sessionId, string billingMonth, CancellationToken ct = default);
    Task<decimal> RefreshOutstandingAsync(Guid enrollmentId, PriceType priceType, Guid sessionId, string billingMonth, CancellationToken ct = default);
}
