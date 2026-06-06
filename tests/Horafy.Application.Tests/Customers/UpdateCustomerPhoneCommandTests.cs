using FluentAssertions;
using Horafy.Application.Features.Customers.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Customers;

public sealed class UpdateCustomerPhoneCommandTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IUserRepository>     _userRepo    = new();
    private readonly Mock<IUnitOfWork>         _uow         = new();

    private UpdateCustomerPhoneCommandHandler MakeHandler() =>
        new(_currentUser.Object, _userRepo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidPhone_UpdatesAndSaves()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)userId);

        var user = User.CreateWithGoogle(
            "j@test.com", "g1", "João", null, Guid.NewGuid(), UserRole.Customer);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var result = await MakeHandler().Handle(
            new UpdateCustomerPhoneCommand("+5511999998888"), default);

        result.IsSuccess.Should().BeTrue();
        user.Phone.Should().Be("+5511999998888");
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_TooLongPhone_ReturnsError()
    {
        var result = await MakeHandler().Handle(
            new UpdateCustomerPhoneCommand(new string('1', 21)), default);

        result.IsFailure.Should().BeTrue();
    }
}
