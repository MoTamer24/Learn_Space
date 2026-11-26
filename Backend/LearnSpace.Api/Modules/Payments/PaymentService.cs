using LearnSpace.Api.Data;
using LearnSpace.Api.Modules.Groups;
using LearnSpace.Api.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Payments;

public class PaymentService(AppDbContext db, IGroupBillingService groups, IStudentDebtService students) : IPaymentService
{
    public async Task<LatestPaymentSnapshot?> GetLatestAsync(Guid enrollmentId, PriceType priceType, Guid sessionId, string billingMonth, CancellationToken ct = default)
    {
        var query = db.Payments.AsNoTracking().Where(x => x.GroupEnrollmentId == enrollmentId && !x.IsDeleted);
        query = priceType == PriceType.Monthly ? query.Where(x => x.BillingMonth == billingMonth) : query.Where(x => x.SessionId == sessionId);
        var payment = await query.OrderByDescending(x => x.PaymentDate).FirstOrDefaultAsync(ct);
        return payment is null ? null : new LatestPaymentSnapshot(payment.Id, payment.AmountPaid, payment.PaymentDate, payment.BillingMonth, payment.SessionId);
    }

    public async Task<decimal> RefreshOutstandingAsync(Guid enrollmentId, PriceType priceType, Guid sessionId, string billingMonth, CancellationToken ct = default)
    {
        var snapshot = await groups.GetBillingSnapshotAsync(enrollmentId, sessionId, ct) ?? throw new KeyNotFoundException("Enrollment not found.");
        if (snapshot.ExemptionType == ExemptionType.Full) { await ApplyBalanceAsync(snapshot, 0, ct); return 0; }
        var query = db.Payments.AsNoTracking().Where(x => x.GroupEnrollmentId == enrollmentId && !x.IsDeleted);
        query = priceType == PriceType.Monthly ? query.Where(x => x.BillingMonth == billingMonth) : query.Where(x => x.SessionId == sessionId);
        var paid = await query.SumAsync(x => (decimal?)x.AmountPaid, ct) ?? 0;
        var due = Math.Max(0, snapshot.Price - snapshot.Discount - paid);
        await ApplyBalanceAsync(snapshot, due, ct);
        return due;
    }

    public async Task<Payment> RecordAsync(RecordPaymentRequest request, CancellationToken ct = default)
    {
        if (request.AmountPaid <= 0) throw new InvalidOperationException("AmountPaid must be greater than zero.");
        var snapshot = await groups.GetBillingSnapshotAsync(request.GroupEnrollmentId, request.SessionId, ct) ?? throw new KeyNotFoundException("Enrollment not found.");
        if (snapshot.ExemptionType == ExemptionType.Full) throw new InvalidOperationException("Fully exempt enrollments do not expect payment.");
        var month = (request.BillingMonth ?? DateTime.UtcNow.ToString("yyyy-MM"));
        if (snapshot.PriceType == PriceType.Monthly && !System.Text.RegularExpressions.Regex.IsMatch(month, "^\\d{4}-(0[1-9]|1[0-2])$")) throw new InvalidOperationException("BillingMonth must use YYYY-MM for monthly groups.");
        if (snapshot.PriceType == PriceType.Session && request.SessionId is null) throw new InvalidOperationException("SessionId is required for per-session groups.");
        var payment = new Payment { GroupEnrollmentId = request.GroupEnrollmentId, AmountPaid = request.AmountPaid, Method = request.Method,
            BillingMonth = snapshot.PriceType == PriceType.Monthly ? month : null, SessionId = snapshot.PriceType == PriceType.Session ? request.SessionId : null };
        db.Payments.Add(payment); await db.SaveChangesAsync(ct);
        await RefreshOutstandingAsync(request.GroupEnrollmentId, snapshot.PriceType, request.SessionId ?? snapshot.SessionId, month, ct);
        return payment;
    }

    private async Task ApplyBalanceAsync(GroupBillingSnapshot snapshot, decimal newBalance, CancellationToken ct)
    {
        newBalance = snapshot.ExemptionType == ExemptionType.Full ? 0 : newBalance;
        await groups.UpdateOutstandingBalanceAsync(snapshot.EnrollmentId, newBalance, ct);
        await students.ApplyEnrollmentBalanceChangeAsync(snapshot.StudentId, newBalance - snapshot.OutstandingBalance, ct);
    }
}
