using FluentAssertions;
using Horafy.Application.Features.Customers.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Customers;

public sealed class GetCustomerProfileQueryTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IUserRepository>     _userRepo    = new();

    private GetCustomerProfileQueryHandler MakeHandler() =>
        new(_currentUser.Object, _userRepo.Object);

    [Fact]
    public async Task Handle_AuthenticatedCustomer_ReturnsProfile()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns((Guid?)userId);

        var user = User.CreateWithGoogle(
            "j@test.com", "g1", "João", null, Guid.NewGuid(), UserRole.Customer);
        user.SetPhone("+5511999998888");
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var result = await MakeHandler().Handle(new GetCustomerProfileQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("j@test.com");
        result.Value.Phone.Should().Be("+5511999998888");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        _currentUser.Setup(c => c.UserId).Returns((Guid?)Guid.NewGuid());
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((User?)null);

        var result = await MakeHandler().Handle(new GetCustomerProfileQuery(), default);

        result.IsFailure.Should().BeTrue();
    }
}
