using LearnSpace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnSpace.Api.Modules.Notifications;

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task NotifyAsync(Guid teacherId, Guid recipientUserId, NotificationType type, string message, CancellationToken ct = default)
    { db.Notifications.Add(new Notification { TeacherId = teacherId, UserId = recipientUserId, Type = type, Message = message }); await db.SaveChangesAsync(ct); }
    public async Task NotifyManyAsync(Guid teacherId, IEnumerable<Guid> recipientUserIds, NotificationType type, string message, CancellationToken ct = default)
    { var ids = recipientUserIds.Distinct().ToArray(); db.Notifications.AddRange(ids.Select(id => new Notification { TeacherId = teacherId, UserId = id, Type = type, Message = message })); await db.SaveChangesAsync(ct); }
    public Task<List<Notification>> GetForTeacherAsync(Guid teacherId, CancellationToken ct = default) => db.Notifications.AsNoTracking().Where(x => x.TeacherId == teacherId).OrderByDescending(x => x.SentAt).ToListAsync(ct);
}
