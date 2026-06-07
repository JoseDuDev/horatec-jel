using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Queries.GetVouchers;

public sealed record GetVouchersQuery : IRequest<Result<IReadOnlyList<VoucherSummary>>>;

public sealed record VoucherSummary(
    Guid                Id,
    string              Code,
    VoucherDiscountType DiscountType,
    decimal             DiscountValue,
    string?             Description,
    DateTimeOffset?     ExpiresAt,
    int?                MaxUses,
    int                 UsedCount,
    bool                IsActive,
    DateTimeOffset      CreatedAt);

internal sealed class GetVouchersQueryHandler(
    IVoucherRepository voucherRepository)
    : IRequestHandler<GetVouchersQuery, Result<IReadOnlyList<VoucherSummary>>>
{
    public async Task<Result<IReadOnlyList<VoucherSummary>>> Handle(GetVouchersQuery request, CancellationToken ct)
    {
        var vouchers = await voucherRepository.GetAllAsync(ct);

        var result = vouchers
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VoucherSummary(
                v.Id, v.Code, v.DiscountType, v.DiscountValue,
                v.Description, v.ExpiresAt, v.MaxUses, v.UsedCount, v.IsActive, v.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<VoucherSummary>>(result);
    }
}
