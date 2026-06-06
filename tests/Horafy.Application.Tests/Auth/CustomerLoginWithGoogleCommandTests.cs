using FluentAssertions;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithGoogle;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Auth;

public sealed class CustomerLoginWithGoogleCommandTests
{
    private readonly Mock<IGoogleOAuthService> _googleOAuth  = new();
    private readonly Mock<IUserRepository>     _userRepo     = new();
    private readonly Mock<ITenantRepository>   _tenantRepo   = new();
    private readonly Mock<ITokenService>       _tokenService = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();

    private CustomerLoginWithGoogleCommandHandler MakeHandler() =>
        new(_googleOAuth.Object, _userRepo.Object, _tenantRepo.Object,
            _tokenService.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidCustomer_ReturnsTokens()
    {
        _googleOAuth.Setup(g => g.ValidateAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new OAuthUserInfo("google_1", "j@test.com", "João", null));

        var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        _tenantRepo.Setup(r => r.GetBySlugAsync("barbearia", default)).ReturnsAsync(tenant);

        _userRepo.Setup(r => r.GetByGoogleIdAsync("google_1", default)).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByEmailAsync("j@test.com", default)).ReturnsAsync((User?)null);

        _tokenService.Setup(t => t.GenerateTokens(It.IsAny<User>()))
            .Returns(new TokenPair("at", "rt",
                DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow.AddDays(7)));

        var result = await MakeHandler().Handle(
            new CustomerLoginWithGoogleCommand("id_token", "barbearia"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoTenantSlug_ReturnsError()
    {
        var result = await MakeHandler().Handle(
            new CustomerLoginWithGoogleCommand("id_token", TenantSlug: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Customer.TenantRequired");
    }

    [Fact]
    public async Task Handle_TenantNotFound_ReturnsError()
    {
        _googleOAuth.Setup(g => g.ValidateAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new OAuthUserInfo("google_1", "j@test.com", "João", null));

        _tenantRepo.Setup(r => r.GetBySlugAsync("unknown", default))
            .ReturnsAsync((Tenant?)null);

        var result = await MakeHandler().Handle(
            new CustomerLoginWithGoogleCommand("id_token", "unknown"), default);

        result.IsFailure.Should().BeTrue();
    }
}
