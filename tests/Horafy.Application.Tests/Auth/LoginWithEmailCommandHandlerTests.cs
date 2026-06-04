using FluentAssertions;
using Horafy.Application.Features.Auth.Commands.LoginWithEmail;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Xunit;
using Horafy.Domain.Interfaces.Repositories;
using Moq;

namespace Horafy.Application.Tests.Auth;

public class LoginWithEmailCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo       = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService>   _tokenService   = new();
    private readonly Mock<IUnitOfWork>     _unitOfWork     = new();

    private LoginWithEmailCommandHandler CreateHandler() =>
        new(_userRepo.Object, _passwordHasher.Object, _tokenService.Object, _unitOfWork.Object);

    // ── Cenário: credenciais válidas ──────────────────────────────────
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokenPair()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hashed_password", "José", null, UserRole.Customer);
        var expectedTokens = new TokenPair("access", "refresh",
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddDays(7));

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
}
