using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Commands.CreateVoucher;

public sealed record CreateVoucherCommand(
    string              Code,
    VoucherDiscountType DiscountType,
    decimal             DiscountValue,
    string?             Description,
    DateTimeOffset?     ExpiresAt,
    int?                MaxUses)
    : IRequest<Result<Guid>>;

internal sealed class CreateVoucherCommandHandler(
    IVoucherRepository voucherRepository,
    ITenantUnitOfWork  unitOfWork)
    : IRequestHandler<CreateVoucherCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateVoucherCommand request, CancellationToken ct)
    {
        if (request.DiscountValue <= 0)
            return Result.Failure<Guid>(VoucherErrors.InvalidDiscountValue);
        if (request.DiscountType == VoucherDiscountType.Percentage && request.DiscountValue > 100)
            return Result.Failure<Guid>(VoucherErrors.InvalidPercentage);

        var exists = await voucherRepository.CodeExistsAsync(request.Code.ToUpperInvariant(), ct);
        if (exists) return Result.Failure<Guid>(VoucherErrors.CodeAlreadyExists);

        var voucher = Voucher.Create(
            request.Code, request.DiscountType, request.DiscountValue,
            request.Description, request.ExpiresAt, request.MaxUses);

        voucherRepository.Add(voucher);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(voucher.Id);
    }
}
