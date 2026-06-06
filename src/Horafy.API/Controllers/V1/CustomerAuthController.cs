using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithApple;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithGoogle;
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
}

public sealed record CustomerGoogleLoginRequest(string IdToken, string TenantSlug);
public sealed record CustomerAppleLoginRequest(string IdentityToken, string TenantSlug);
