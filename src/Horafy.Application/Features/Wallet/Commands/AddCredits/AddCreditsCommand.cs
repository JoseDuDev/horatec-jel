using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;
using WalletEntity = Horafy.Domain.Entities.Wallet.Wallet;
using WalletTransactionType = Horafy.Domain.Entities.Wallet.WalletTransactionType;

namespace Horafy.Application.Features.Wallet.Commands.AddCredits;

public sealed record AddCreditsCommand(Guid UserId, decimal Amount, string Description)
    : IRequest<Result>;

internal sealed class AddCreditsCommandHandler(
    IWalletRepository walletRepository,
    ITenantUnitOfWork unitOfWork)
    : IRequestHandler<AddCreditsCommand, Result>
{
    public async Task<Result> Handle(AddCreditsCommand request, CancellationToken ct)
    {
        var wallet = await walletRepository.GetByUserIdAsync(request.UserId, ct);

        if (wallet is null)
        {
            wallet = WalletEntity.Create(request.UserId);
            walletRepository.Add(wallet);
        }

        var result = wallet.AddCredits(request.Amount, request.Description);
        if (result.IsFailure) return result;

        walletRepository.Update(wallet);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
