using FluentAssertions;
using Horafy.Application.Features.Wallet.Commands.AddCredits;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Wallet;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Wallet;

public sealed class AddCreditsCommandHandlerTests
{
    private readonly Mock<IWalletRepository> _repo = new();
    private readonly Mock<ITenantUnitOfWork>  _uow  = new();

    private AddCreditsCommandHandler MakeHandler() => new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NewUser_CreatesWalletAndAddsCredits()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((Domain.Entities.Wallet.Wallet?)null);

        var result = await MakeHandler().Handle(
            new AddCreditsCommand(userId, 50m, "Bônus de boas-vindas"), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.Is<Domain.Entities.Wallet.Wallet>(w => w.UserId == userId && w.Balance == 50m)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingWallet_AccumulatesBalance()
    {
        var userId = Guid.NewGuid();
        var wallet = Domain.Entities.Wallet.Wallet.Create(userId);
        wallet.AddCredits(30m, "Crédito inicial");

        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(wallet);

        var result = await MakeHandler().Handle(
            new AddCreditsCommand(userId, 20m, "Crédito extra"), default);

        result.IsSuccess.Should().BeTrue();
        wallet.Balance.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_NegativeAmount_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((Domain.Entities.Wallet.Wallet?)null);

        var result = await MakeHandler().Handle(
            new AddCreditsCommand(userId, -10m, "Inválido"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Wallet.InvalidAmount");
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
