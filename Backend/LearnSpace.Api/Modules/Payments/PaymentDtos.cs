using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Payments;

public record RecordPaymentRequest(Guid GroupEnrollmentId, [property: Range(0.01, 10000000)] decimal AmountPaid,
    PaymentMethod Method, string? BillingMonth, Guid? SessionId);
