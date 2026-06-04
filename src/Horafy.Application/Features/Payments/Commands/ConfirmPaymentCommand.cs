using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Commands;

public sealed record ConfirmPaymentCommand(string MpPaymentId) : IRequest<Result>;

internal sealed class ConfirmPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<ConfirmPaymentCommand, Result>
{
    public async Task<Result> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        // Idempotency: if MpPaymentId already processed, return success
        var existing = await paymentRepository.GetByMpPaymentIdAsync(request.MpPaymentId, cancellationToken);
        if (existing is not null) return Result.Success();

        var mpStatus = await gateway.GetPaymentStatusAsync(request.MpPaymentId, cancellationToken);

        var payment = await paymentRepository.GetByPreferenceIdAsync(mpStatus.PreferenceId, cancellationToken);
        if (payment is null) return Result.Failure(PaymentErrors.NotFound);

        if (mpStatus.Status == PaymentStatus.Approved)
            payment.Approve(request.MpPaymentId);
        else if (mpStatus.Status is PaymentStatus.Rejected or PaymentStatus.Cancelled)
            payment.Reject(request.MpPaymentId);

        paymentRepository.Update(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
