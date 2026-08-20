using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Auth.Commands.CreatePlatformAdmin;
using Horafy.Application.Features.Auth.Commands.ForgotPassword;
using Horafy.Application.Features.Auth.Commands.LoginWithApple;
using Horafy.Application.Features.Auth.Commands.LoginWithEmail;
using Horafy.Application.Features.Auth.Commands.LoginWithGoogle;
using Horafy.Application.Features.Auth.Commands.RefreshToken;
using Horafy.Application.Features.Auth.Commands.ResetPassword;
using Horafy.Application.Features.Auth.Queries.GetCurrentUser;
using Horafy.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class AuthController(ISender sender) : ApiControllerBase(sender)
{
    /// <summary>Login com Google — valida o ID token emitido pelo SDK do Google no cliente.</summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginWithGoogle(
        [FromBody] LoginWithGoogleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new LoginWithGoogleCommand(request.IdToken, request.TenantSlug), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Login com Apple — valida o identity token emitido pelo Sign in with Apple SDK.</summary>
    [HttpPost("apple")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginWithApple(
        [FromBody] LoginWithAppleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new LoginWithAppleCommand(request.IdentityToken, request.TenantSlug), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Login com e-mail e senha.</summary>
    [HttpPost("email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginWithEmail(
        [FromBody] LoginWithEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new LoginWithEmailCommand(request.Email, request.Password, request.TenantSlug),
            cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Cadastro com e-mail e senha.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterWithEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new Application.Features.Auth.Commands.RegisterWithEmail.RegisterWithEmailCommand(
                request.Email, request.Password, request.Name, request.TenantSlug),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Solicita redefinição de senha — sempre responde com sucesso genérico (anti-enumeration).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Redefine a senha a partir do token recebido por e-mail.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Cria um novo PlatformAdmin (superadmin) — apenas outro PlatformAdmin pode.</summary>
    [HttpPost("/api/v{version:apiVersion}/platform/admins")]
    [Authorize(Roles = "PlatformAdmin")]
    [ProducesResponseType(typeof(CreatePlatformAdminResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePlatformAdmin(
        [FromBody] CreatePlatformAdminRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreatePlatformAdminCommand(request.Email, request.Password, request.Name),
            cancellationToken);

        if (result.IsFailure) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Renova access token usando um refresh token válido.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Retorna os dados do usuário autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCurrentUserQuery(), cancellationToken);
        return ToActionResult(result);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record LoginWithGoogleRequest(string IdToken, string? TenantSlug);
public sealed record LoginWithAppleRequest(string IdentityToken, string? TenantSlug);
public sealed record LoginWithEmailRequest(string Email, string Password, string? TenantSlug);
public sealed record RegisterWithEmailRequest(string Email, string Password, string Name, string? TenantSlug);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record CreatePlatformAdminRequest(string Email, string Password, string Name);
