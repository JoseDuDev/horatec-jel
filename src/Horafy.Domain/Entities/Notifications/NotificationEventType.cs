namespace Horafy.Domain.Entities.Notifications;

public enum NotificationEventType
{
    BookingCreated   = 0,
    BookingConfirmed = 1,
    BookingCancelled = 2,
    BookingReminder  = 3,
    PaymentPending   = 4,
    PaymentConfirmed = 5
}
