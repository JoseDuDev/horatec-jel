using FluentAssertions;
using Horafy.Application.Features.Vouchers.Commands.CreateVoucher;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Vouchers;

public sealed class CreateVoucherCommandHandlerTests
{
    private readonly Mock<IVoucherRepository> _repo = new();
    private readonly Mock<ITenantUnitOfWork>  _uow  = new();

    private CreateVoucherCommandHandler MakeHandler() => new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesVoucher()
    {
        _repo.Setup(r => r.CodeExistsAsync("PROMO20", default)).ReturnsAsync(false);

        var result = await MakeHandler().Handle(
            new CreateVoucherCommand("PROMO20", VoucherDiscountType.Percentage, 20m, null, null, null),
            default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.Is<Voucher>(v => v.Code == "PROMO20" && v.DiscountValue == 20m)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateCode_ReturnsConflict()
    {
        _repo.Setup(r => r.CodeExistsAsync("PROMO20", default)).ReturnsAsync(true);

        var result = await MakeHandler().Handle(
            new CreateVoucherCommand("PROMO20", VoucherDiscountType.Fixed, 10m, null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.CodeAlreadyExists");
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_PercentageOver100_ReturnsValidationError()
    {
        var result = await MakeHandler().Handle(
            new CreateVoucherCommand("FULL", VoucherDiscountType.Percentage, 150m, null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.InvalidPercentage");
    }
}
