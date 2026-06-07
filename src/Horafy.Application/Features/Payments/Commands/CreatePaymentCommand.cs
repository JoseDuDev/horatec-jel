using FluentValidation;
using Horafy.Application.Features.Bookings;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;

namespace Horafy.Application.Features.Payments.Commands;

public sealed record CreatePaymentCommand(
    Guid          BookingId,
    decimal       Amount,
    PaymentMethod Method,
    string        BackUrl,
    string?       VoucherCode      = null,
    bool          UseWalletCredits = false) : IRequest<Result<CreatePaymentResult>>;

public sealed record CreatePaymentResult(Guid PaymentId, string PreferenceId, string? PaymentUrl);

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.BackUrl).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.VoucherCode).MaximumLength(50).When(x => x.VoucherCode is not null);
    }
}

internal sealed class CreatePaymentCommandHandler(
    IBookingRepository    bookingRepository,
    ITenantRepository     tenantRepository,
    IPaymentRepository    paymentRepository,
    IPaymentGateway       gateway,
    ICurrentTenantService currentTenant,
    ITenantUnitOfWork     unitOfWork,
    IVoucherRepository    voucherRepository,
    IWalletRepository     walletRepository,
    ICurrentUserService   currentUser) : IRequestHandler<CreatePaymentCommand, Result<CreatePaymentResult>>
{
    public async Task<Result<CreatePaymentResult>> Handle(
        CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure<CreatePaymentResult>(BookingErrors.NotFound);

        // --- Desconto de voucher ---
        decimal voucherDiscount = 0;
        Voucher? voucher = null;
        if (request.VoucherCode is not null)
        {
            voucher = await voucherRepository.GetByCodeAsync(
                request.VoucherCode.ToUpperInvariant(), cancellationToken);
            if (voucher is null)
                return Result.Failure<CreatePaymentResult>(VoucherErrors.NotFound);

            var discResult = voucher.CalculateDiscount(request.Amount);
            if (discResult.IsFailure)
                return Result.Failure<CreatePaymentResult>(discResult.Error);

            voucherDiscount = discResult.Value;
        }

        // --- Débito da carteira ---
        decimal walletDebit = 0;
        WalletEntity? wallet = null;
        if (request.UseWalletCredits && currentUser.UserId.HasValue)
        {
            wallet = await walletRepository.GetByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (wallet is not null && wallet.Balance > 0)
            {
                var remaining = request.Amount - voucherDiscount;
                walletDebit = Math.Min(wallet.Balance, remaining);
                if (walletDebit < 0) walletDebit = 0;
            }
        }

        // --- Valor líquido para o gateway ---
        var netAmount = request.Amount - voucherDiscount - walletDebit;

        Payment payment;

        if (netAmount <= 0)
        {
            // Pagamento coberto integralmente por wallet/voucher — sem MP
            payment = Payment.Create(
                booking.Id,
                preferenceId: "wallet-voucher-coverage",
                request.Method,
                request.Amount,
                depositAmount: 0,
                paymentUrl: null,
                expiresAt: null,
                voucherCode: request.VoucherCode,
                voucherDiscountAmount: voucherDiscount,
                walletAmount: walletDebit);

            payment.ApproveDirectly();
        }
        else
        {
            // Calcular depósito parcial somente quando não há voucher/wallet aplicado
            var depositAmount = 0m;
            if (voucher is null && !request.UseWalletCredits && currentTenant.TenantId.HasValue)
            {
                var tenant = await tenantRepository.GetByIdAsync(
                    currentTenant.TenantId.Value, cancellationToken);
                if (tenant?.PaymentSettings.RequiresPayment is true)
                    depositAmount = tenant.PaymentSettings.CalculateDepositAmount(netAmount);
            }

            var webhookUrl = $"{request.BackUrl.TrimEnd('/')}/webhooks/mercadopago";
            var prefResult = await gateway.CreatePreferenceAsync(
                new CreatePaymentPreferenceRequest(
                    booking.Id, netAmount, depositAmount,
                    request.Method, booking.CustomerEmail,
                    request.BackUrl, webhookUrl),
                cancellationToken);

            payment = Payment.Create(
                booking.Id, prefResult.PreferenceId, request.Method,
                request.Amount, depositAmount,
                prefResult.PaymentUrl, prefResult.ExpiresAt,
                voucherCode: request.VoucherCode,
                voucherDiscountAmount: voucherDiscount,
                walletAmount: walletDebit);
        }

        // Aplicar voucher e carteira
        if (voucher is not null)
        {
            voucher.Redeem();
            voucherRepository.Update(voucher);
        }

        if (wallet is not null && walletDebit > 0)
        {
            var debitResult = wallet.DebitPayment(
                walletDebit, $"Agendamento #{booking.Id.ToString()[..8]}", booking.Id);
            if (debitResult.IsFailure) return Result.Failure<CreatePaymentResult>(debitResult.Error);
            walletRepository.Update(wallet);
        }

        paymentRepository.Add(payment);
        booking.MarkPaymentPending();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreatePaymentResult(payment.Id, payment.PreferenceId, payment.PaymentUrl));
    }
}
