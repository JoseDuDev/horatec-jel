using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Commands.DeactivateVoucher;

public sealed record DeactivateVoucherCommand(Guid Id) : IRequest<Result>;

internal sealed class DeactivateVoucherCommandHandler(
    IVoucherRepository voucherRepository,
    ITenantUnitOfWork  unitOfWork)
    : IRequestHandler<DeactivateVoucherCommand, Result>
{
    public async Task<Result> Handle(DeactivateVoucherCommand request, CancellationToken ct)
    {
        var voucher = await voucherRepository.GetByIdAsync(request.Id, ct);
        if (voucher is null) return Result.Failure(VoucherErrors.NotFound);

        voucher.Deactivate();
        voucherRepository.Update(voucher);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
