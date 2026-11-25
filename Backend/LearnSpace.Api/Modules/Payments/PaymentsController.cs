using LearnSpace.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;
using LearnSpace.Api.Modules.Groups;

namespace LearnSpace.Api.Modules.Payments;

[ApiController, Route("api/payments"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher + "," + AppRoles.Staff)]
public class PaymentsController(AppDbContext db, PaymentService service, IGroupBillingService groups, IStaffDirectory staff) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct)
    {
        var payments = await db.Payments.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.PaymentDate).ToListAsync(ct);
        if (User.IsInRole(AppRoles.Admin)) return Ok(payments);
        var visible = new List<Payment>();
        foreach (var payment in payments)
            if ((await groups.GetBillingSnapshotAsync(payment.GroupEnrollmentId, payment.SessionId, ct))?.TeacherId == await CurrentTeacherIdAsync(ct)) visible.Add(payment);
        return Ok(visible);
    }
    [HttpPost] public async Task<IActionResult> Record(RecordPaymentRequest request, CancellationToken ct)
    { var snapshot = await groups.GetBillingSnapshotAsync(request.GroupEnrollmentId, request.SessionId, ct); if (snapshot is null) return NotFound(); if (!User.IsInRole(AppRoles.Admin) && snapshot.TeacherId != await CurrentTeacherIdAsync(ct)) return Forbid(); try { var payment = await service.RecordAsync(request, ct); return Created($"/api/payments/{payment.Id}", payment); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var p = await db.Payments.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (p is null) return NotFound();
        var snapshot = await groups.GetBillingSnapshotAsync(p.GroupEnrollmentId, p.SessionId, ct);
        if (snapshot is null) return NotFound();
        if (!User.IsInRole(AppRoles.Admin) && snapshot.TeacherId != await CurrentTeacherIdAsync(ct)) return Forbid();
        p.IsDeleted = true; p.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        if (snapshot is not null)
            await service.RefreshOutstandingAsync(p.GroupEnrollmentId, snapshot.PriceType, p.SessionId ?? snapshot.SessionId, p.BillingMonth ?? DateTime.UtcNow.ToString("yyyy-MM"), ct);
        return NoContent();
    }
    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
    private async Task<Guid> CurrentTeacherIdAsync(CancellationToken ct) => User.IsInRole(AppRoles.Staff) ? await staff.GetTeacherIdForStaffUserAsync(CurrentUserId(), ct) ?? Guid.Empty : CurrentUserId();
}
