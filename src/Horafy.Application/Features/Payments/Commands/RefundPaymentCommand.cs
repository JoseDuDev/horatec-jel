using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Commands;

public sealed record RefundPaymentCommand(Guid PaymentId, decimal? Amount) : IRequest<Result>;

internal sealed class RefundPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<RefundPaymentCommand, Result>
{
    public async Task<Result> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment is null) return Result.Failure(PaymentErrors.NotFound);

        if (payment.Status != Domain.Entities.Payments.PaymentStatus.Approved)
            return Result.Failure(PaymentErrors.NotApproved);

        var refundAmount = request.Amount ?? payment.Amount;
        var refundResult = await gateway.RefundAsync(payment.MpPaymentId!, refundAmount, cancellationToken);
        if (!refundResult.Success) return Result.Failure(PaymentErrors.RefundFailed);

        payment.Refund();
        paymentRepository.Update(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
