using Horafy.Domain.Events.Bookings;
using MediatR;

namespace Horafy.Application.Features.Integrations.Webhooks;

/// <summary>
/// Handlers que reagem aos eventos de domínio de booking e disparam o webhook de saída.
/// São aditivos aos publishers de notificação existentes (MediatR publica para todos).
/// </summary>
internal sealed class BookingCreatedWebhookHandler(IntegrationWebhookDispatcher dispatcher)
    : INotificationHandler<BookingCreatedEvent>
{
    // created: pula origem de integração (Atendefy já conhece a reserva que criou).
    public Task Handle(BookingCreatedEvent n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.BookingId, "booking.created", skipIntegrationOrigin: true, ct);
}

internal sealed class BookingConfirmedWebhookHandler(IntegrationWebhookDispatcher dispatcher)
    : INotificationHandler<BookingConfirmedEvent>
{
    public Task Handle(BookingConfirmedEvent n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.BookingId, "booking.confirmed", skipIntegrationOrigin: false, ct);
}

internal sealed class BookingCancelledWebhookHandler(IntegrationWebhookDispatcher dispatcher)
    : INotificationHandler<BookingCancelledEvent>
{
    public Task Handle(BookingCancelledEvent n, CancellationToken ct) =>
        dispatcher.DispatchAsync(n.BookingId, "booking.cancelled", skipIntegrationOrigin: false, ct);
}
