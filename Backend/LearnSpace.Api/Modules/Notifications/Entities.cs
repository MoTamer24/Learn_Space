namespace LearnSpace.Api.Modules.Notifications;

public enum NotificationType { Absence, Session, LatePayment }
public enum NotificationChannel { InApp, Email, Sms }
public enum NotificationStatus { Pending, Sent, Failed }

public class Notification
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = "";
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public NotificationStatus Status { get; set; } = NotificationStatus.Sent;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public interface INotificationService
{
    Task NotifyAsync(Guid teacherId, Guid recipientUserId, NotificationType type, string message, CancellationToken ct = default);
    Task NotifyManyAsync(Guid teacherId, IEnumerable<Guid> recipientUserIds, NotificationType type, string message, CancellationToken ct = default);
}
