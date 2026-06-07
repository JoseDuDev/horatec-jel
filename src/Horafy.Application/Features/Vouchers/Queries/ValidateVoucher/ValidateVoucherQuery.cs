using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Queries.ValidateVoucher;

public sealed record ValidateVoucherQuery(string Code, decimal TotalPrice)
    : IRequest<Result<VoucherValidationResult>>;

public sealed record VoucherValidationResult(
    string              Code,
    VoucherDiscountType DiscountType,
    decimal             DiscountValue,
    decimal             DiscountAmount,
    decimal             FinalPrice,
    string?             Description);

internal sealed class ValidateVoucherQueryHandler(
    IVoucherRepository voucherRepository)
    : IRequestHandler<ValidateVoucherQuery, Result<VoucherValidationResult>>
{
    public async Task<Result<VoucherValidationResult>> Handle(ValidateVoucherQuery request, CancellationToken ct)
    {
        var voucher = await voucherRepository.GetByCodeAsync(request.Code.ToUpperInvariant(), ct);
        if (voucher is null)
            return Result.Failure<VoucherValidationResult>(VoucherErrors.NotFound);

        var discountResult = voucher.CalculateDiscount(request.TotalPrice);
        if (discountResult.IsFailure)
            return Result.Failure<VoucherValidationResult>(discountResult.Error);

        return Result.Success(new VoucherValidationResult(
            voucher.Code,
            voucher.DiscountType,
            voucher.DiscountValue,
            discountResult.Value,
            request.TotalPrice - discountResult.Value,
            voucher.Description));
    }
}
