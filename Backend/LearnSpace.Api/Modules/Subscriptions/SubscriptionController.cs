using LearnSpace.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;

namespace LearnSpace.Api.Modules.Subscriptions;

public record CreatePlanRequest(string Name, decimal Price, int MaxStudents, int MaxGroups, decimal MaxStorageMB);
public record CreateSubscriptionRequest(Guid TeacherId, Guid PlanId, DateOnly StartDate, DateOnly EndDate);
public record CreateTeacherPaymentRequest(Guid TeacherSubscriptionId, string BillingMonth, decimal Amount, TeacherPaymentStatus Status);

[ApiController, Route("api/subscriptions"), Authorize(Roles = AppRoles.Admin + "," + AppRoles.Teacher)]
public class SubscriptionController(AppDbContext db) : ControllerBase
{
    [HttpGet("plans")] public async Task<IActionResult> Plans(CancellationToken ct) => Ok(await db.Plans.AsNoTracking().OrderBy(x => x.Price).ToListAsync(ct));
    [HttpPost("plans"), Authorize(Roles = AppRoles.Admin)] public async Task<IActionResult> AddPlan(CreatePlanRequest r, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(r.Name) || r.Price < 0 || r.MaxStudents < 0 || r.MaxGroups < 0 || r.MaxStorageMB < 0) return BadRequest(); var x = new Plan { Name = r.Name.Trim(), Price = r.Price, MaxStudents = r.MaxStudents, MaxGroups = r.MaxGroups, MaxStorageMB = r.MaxStorageMB }; db.Plans.Add(x); await db.SaveChangesAsync(ct); return Created($"/api/subscriptions/plans/{x.Id}", x); }
    [HttpGet("teachers/{teacherId:guid}")] public async Task<IActionResult> ForTeacher(Guid teacherId, CancellationToken ct) { if (!User.IsInRole(AppRoles.Admin) && CurrentUserId() != teacherId) return Forbid(); return Ok(await db.TeacherSubscriptions.AsNoTracking().Where(x => x.TeacherId == teacherId).ToListAsync(ct)); }
    [HttpPost] public async Task<IActionResult> Subscribe(CreateSubscriptionRequest r, CancellationToken ct)
    { if (r.EndDate < r.StartDate || !await db.Plans.AnyAsync(x => x.Id == r.PlanId, ct)) return BadRequest(); var teacherId = User.IsInRole(AppRoles.Admin) ? r.TeacherId : CurrentUserId(); var x = new TeacherSubscription { TeacherId = teacherId, PlanId = r.PlanId, StartDate = r.StartDate, EndDate = r.EndDate }; db.TeacherSubscriptions.Add(x); await db.SaveChangesAsync(ct); return Created($"/api/subscriptions/{x.Id}", x); }
    [HttpPost("payments")] public async Task<IActionResult> Payment(CreateTeacherPaymentRequest r, CancellationToken ct)
    { if (r.Amount <= 0 || !System.Text.RegularExpressions.Regex.IsMatch(r.BillingMonth, "^\\d{4}-(0[1-9]|1[0-2])$")) return BadRequest(new { error = "Provide a positive amount and BillingMonth in YYYY-MM format." }); var owned = await db.TeacherSubscriptions.AnyAsync(x => x.Id == r.TeacherSubscriptionId && (User.IsInRole(AppRoles.Admin) || x.TeacherId == CurrentUserId()), ct); if (!owned) return NotFound(); var x = new TeacherPayment { TeacherSubscriptionId = r.TeacherSubscriptionId, BillingMonth = r.BillingMonth, Amount = r.Amount, Status = r.Status }; db.TeacherPayments.Add(x); await db.SaveChangesAsync(ct); return Created($"/api/subscriptions/payments/{x.Id}", x); }
    [HttpGet("payments")] public async Task<IActionResult> Payments(CancellationToken ct) => Ok(await db.TeacherPayments.AsNoTracking().Where(p => User.IsInRole(AppRoles.Admin) || db.TeacherSubscriptions.Any(s => s.Id == p.TeacherSubscriptionId && s.TeacherId == CurrentUserId())).OrderByDescending(x => x.PaymentDate).ToListAsync(ct));
    private Guid CurrentUserId() => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
