using FluentAssertions;
using Horafy.Application.Features.Auth.Commands.LoginWithEmail;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Xunit;
using Horafy.Domain.Interfaces.Repositories;
using Moq;

namespace Horafy.Application.Tests.Auth;

public class LoginWithEmailCommandHandlerTests
{
    private readonly Mock<IUserRepository>   _userRepo       = new();
    private readonly Mock<ITenantRepository> _tenantRepo     = new();
    private readonly Mock<IPasswordHasher>   _passwordHasher = new();
    private readonly Mock<ITokenService>     _tokenService   = new();
    private readonly Mock<IUnitOfWork>       _unitOfWork     = new();

    private LoginWithEmailCommandHandler CreateHandler() =>
        new(_userRepo.Object, _tenantRepo.Object, _passwordHasher.Object,
            _tokenService.Object, _unitOfWork.Object);

    private static TokenPair MakeTokens() =>
        new("access", "refresh",
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddDays(7));

    // ── Cenário: credenciais válidas ──────────────────────────────────
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokenPair()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hashed_password", "José", null, UserRole.Customer);
        var expectedTokens = MakeTokens();

        _userRepo.Setup(r => r.GetByEmailAsync("jose@gmail.com", default))
                 .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("senha123", "hashed_password"))
                       .Returns(true);
        _tokenService.Setup(t => t.GenerateTokens(user))
                     .Returns(expectedTokens);

        var handler = CreateHandler();
        var result  = await handler.Handle(
            new LoginWithEmailCommand("jose@gmail.com", "senha123", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedTokens);
    }

    // ── Cenário: e-mail não existe ────────────────────────────────────
    [Fact]
    public async Task Handle_EmailNotFound_ReturnsInvalidCredentialsError()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("naoexiste@gmail.com", "qualquer", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    // ── Cenário: senha incorreta ──────────────────────────────────────
    [Fact]
    public async Task Handle_WrongPassword_ReturnsInvalidCredentialsError()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hashed_password", "José", null, UserRole.Customer);

        _userRepo.Setup(r => r.GetByEmailAsync("jose@gmail.com", default))
                 .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("errada", "hashed_password"))
                       .Returns(false);

        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("jose@gmail.com", "errada", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    // ── Cenário: usuário sem senha (OAuth only) ───────────────────────
    [Fact]
    public async Task Handle_UserWithoutPassword_ReturnsInvalidCredentials()
    {
        var user = User.CreateWithGoogle("jose@gmail.com", "google-id", "José", null, null, UserRole.Customer);

        _userRepo.Setup(r => r.GetByEmailAsync("jose@gmail.com", default))
                 .ReturnsAsync(user);

        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("jose@gmail.com", "qualquer", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    // ── Verifica que GenerateTokens é chamado apenas em sucesso ───────
    [Fact]
    public async Task Handle_ValidCredentials_CallsGenerateTokensOnce()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);

        _userRepo.Setup(r => r.GetByEmailAsync("jose@gmail.com", default)).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify("senha", "hash")).Returns(true);
        _tokenService.Setup(t => t.GenerateTokens(user))
                     .Returns(new TokenPair("a", "r", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        await CreateHandler().Handle(
            new LoginWithEmailCommand("jose@gmail.com", "senha", null), default);

        _tokenService.Verify(t => t.GenerateTokens(user), Times.Once);
    }

    // ── Cenário: slug informado e usuário de OUTRO tenant → falha ─────
    [Fact]
    public async Task Handle_EmailWithMismatchedTenantSlug_ReturnsInvalidCredentials()
    {
        var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        var user   = User.CreateWithEmail(
            "jose@gmail.com", "hash", "José", Guid.NewGuid(), UserRole.Customer);

        _tenantRepo.Setup(r => r.GetBySlugAsync("barbearia", default)).ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync("jose@gmail.com", default)).ReturnsAsync(user);

        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("jose@gmail.com", "senha", "barbearia"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        _tokenService.Verify(t => t.GenerateTokens(It.IsAny<User>()), Times.Never);
    }

    // ── Cenário: slug informado + PlatformAdmin (sem tenant) → passa ──
    [Fact]
    public async Task Handle_PlatformAdminWithTenantSlug_StillLogsIn()
    {
        var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        var admin  = User.CreateWithEmail(
            "admin@horafy.com", "hash", "Admin", null, UserRole.PlatformAdmin);

        _tenantRepo.Setup(r => r.GetBySlugAsync("barbearia", default)).ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync("admin@horafy.com", default)).ReturnsAsync(admin);
        _passwordHasher.Setup(h => h.Verify("senha", "hash")).Returns(true);
        _tokenService.Setup(t => t.GenerateTokens(admin)).Returns(MakeTokens());

        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("admin@horafy.com", "senha", "barbearia"), default);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Cenário: login por celular válido ─────────────────────────────
    [Fact]
    public async Task Handle_PhoneLogin_SingleMatch_ReturnsTokenPair()
    {
        var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        var user   = User.CreateWithEmail("jose@gmail.com", "hash", "José", tenant.Id, UserRole.Customer);
        user.SetPhone("47988572233");

        _tenantRepo.Setup(r => r.GetBySlugAsync("barbearia", default)).ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByPhoneAsync(
                It.Is<IReadOnlyCollection<string>>(c =>
                    c.Contains("47988572233") && c.Contains("5547988572233")),
                tenant.Id, default))
            .ReturnsAsync([user]);
        _passwordHasher.Setup(h => h.Verify("senha", "hash")).Returns(true);
        _tokenService.Setup(t => t.GenerateTokens(user)).Returns(MakeTokens());

        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("(47) 98857-2233", "senha", "barbearia"), default);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Cenário: celular sem TenantSlug → falha ───────────────────────
    [Fact]
    public async Task Handle_PhoneLoginWithoutTenantSlug_ReturnsInvalidCredentials()
    {
        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("47988572233", "senha", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        _userRepo.Verify(r => r.GetByPhoneAsync(
            It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid>(), default), Times.Never);
    }

    // ── Cenário: celular ambíguo (mais de um usuário) → falha ─────────
    [Fact]
    public async Task Handle_PhoneLoginWithMultipleMatches_ReturnsInvalidCredentials()
    {
        var tenant = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        var u1 = User.CreateWithEmail("a@x.com", "hash", "A", tenant.Id, UserRole.Customer);
        var u2 = User.CreateWithEmail("b@x.com", "hash", "B", tenant.Id, UserRole.Customer);

        _tenantRepo.Setup(r => r.GetBySlugAsync("barbearia", default)).ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByPhoneAsync(
                It.IsAny<IReadOnlyCollection<string>>(), tenant.Id, default))
            .ReturnsAsync([u1, u2]);

        var result = await CreateHandler().Handle(
            new LoginWithEmailCommand("47988572233", "senha", "barbearia"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }
}
