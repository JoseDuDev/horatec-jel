using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Wallet;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Wallet.Queries.GetWallet;

public sealed record GetWalletQuery : IRequest<Result<WalletResult>>;

public sealed record WalletResult(
    Guid WalletId,
    decimal Balance,
    IReadOnlyList<WalletTransactionResult> Transactions);

public sealed record WalletTransactionResult(
    Guid Id,
    WalletTransactionType Type,
    decimal Amount,
    string Description,
    Guid? BookingId,
    DateTimeOffset CreatedAt);

internal sealed class GetWalletQueryHandler(
    IWalletRepository walletRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetWalletQuery, Result<WalletResult>>
{
    public async Task<Result<WalletResult>> Handle(GetWalletQuery request, CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Result.Failure<WalletResult>(Error.Unauthorized);

        var wallet = await walletRepository.GetByUserIdAsync(currentUser.UserId.Value, ct);

        if (wallet is null)
            return Result.Success(new WalletResult(Guid.Empty, 0, Array.Empty<WalletTransactionResult>()));

        var transactions = wallet.Transactions
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new WalletTransactionResult(t.Id, t.Type, t.Amount, t.Description, t.BookingId, t.CreatedAt))
            .ToList();

        return Result.Success(new WalletResult(wallet.Id, wallet.Balance, transactions));
    }
}
