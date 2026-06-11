using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithApple;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithGoogle;
using Horafy.Application.Features.Auth.Commands.CustomerTestLogin;
using Horafy.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/customers/auth")]
public sealed class CustomerAuthController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpPost("google")]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Google(
        [FromBody] CustomerGoogleLoginRequest request, CancellationToken ct) =>
        ToActionResult(await Sender.Send(
            new CustomerLoginWithGoogleCommand(request.IdToken, request.TenantSlug), ct));

    [HttpPost("apple")]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Apple(
        [FromBody] CustomerAppleLoginRequest request, CancellationToken ct) =>
        ToActionResult(await Sender.Send(
            new CustomerLoginWithAppleCommand(request.IdentityToken, request.TenantSlug), ct));

    /// <summary>
    /// Login de teste para clientes — cria ou recupera usuário sem OAuth.
    /// Retorna 404 fora do ambiente E2ETest.
    /// </summary>
    [HttpPost("test-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestLogin(
        [FromBody] CustomerTestLoginRequest request, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "E2ETest")
            return NotFound();

        return ToActionResult(await Sender.Send(
            new CustomerTestLoginCommand(request.Email, request.TenantSlug), ct));
    }
}

public sealed record CustomerGoogleLoginRequest(string IdToken, string TenantSlug);
public sealed record CustomerAppleLoginRequest(string IdentityToken, string TenantSlug);
public sealed record CustomerTestLoginRequest(string Email, string TenantSlug);
