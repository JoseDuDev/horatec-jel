using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MediatR;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;

namespace Horafy.Application.Features.Bookings.EventHandlers;

internal sealed class BookingCompletedEventHandler(
    ITenantRepository     tenantRepository,
    IPaymentRepository    paymentRepository,
    IWalletRepository     walletRepository,
    ICurrentTenantService currentTenant,
    ITenantUnitOfWork     unitOfWork)
    : INotificationHandler<BookingCompletedEvent>
{
    public async Task Handle(BookingCompletedEvent notification, CancellationToken cancellationToken)
    {
        if (!currentTenant.TenantId.HasValue) return;

        var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
        if (tenant is null || !tenant.LoyaltySettings.IsEnabled) return;

        var payment = await paymentRepository.GetByBookingIdAsync(notification.BookingId, cancellationToken);
        if (payment?.Status != PaymentStatus.Approved) return;

        var bonus = tenant.LoyaltySettings.CalculateBonus(payment.Amount);
        if (bonus <= 0) return;

        var wallet = await walletRepository.GetByUserIdAsync(notification.CustomerId, cancellationToken);
        var isNew  = wallet is null;

        if (isNew)
            wallet = WalletEntity.Create(notification.CustomerId);

        wallet!.AddLoyaltyBonus(
            bonus,
            $"Bônus de fidelidade — #{notification.BookingId.ToString()[..8]}",
            notification.BookingId);

        if (isNew)
            walletRepository.Add(wallet);
        else
            walletRepository.Update(wallet);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
