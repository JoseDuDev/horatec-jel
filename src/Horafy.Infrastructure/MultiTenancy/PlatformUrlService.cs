using Horafy.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Horafy.Infrastructure.MultiTenancy;

/// <summary>
/// Tira o host do header `Origin` da requisição, que é o host da marca que o
/// usuário está usando de fato (agenda.mjml.com.br, alugue.mjml.com.br ou o
/// subdomínio do tenant em qualquer uma das duas).
///
/// A origem é **validada contra `Platform:Domains`** antes de entrar no link:
/// sem isso, bastaria um `Origin` forjado num POST de "esqueci minha senha" para
/// fazer a plataforma enviar, ao e-mail real do usuário, um link de redefinição
/// apontando para o domínio de quem forjou — com o token válido na query string.
/// </summary>
internal sealed class PlatformUrlService(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : IPlatformUrlService
{
    private readonly string[] _platformDomains =
        (configuration.GetSection("Platform:Domains").Get<string[]>() is { Length: > 0 } list
            ? list
            : [configuration["Platform:Domain"] ?? string.Empty])
        .Where(d => !string.IsNullOrWhiteSpace(d))
        .Select(d => d.Trim().ToLowerInvariant())
        .ToArray();

    private readonly string _fallbackDomain =
        configuration["Platform:Domain"]?.Trim().ToLowerInvariant() ?? string.Empty;

    public string BuildUrl(string path)
    {
        var host = ResolveHostFromOrigin() ?? _fallbackDomain;
        return $"https://{host}/{path.TrimStart('/')}";
    }

    private string? ResolveHostFromOrigin()
    {
        var origin = httpContextAccessor.HttpContext?.Request.Headers.Origin.ToString();

        if (string.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            return null;

        var host = uri.Host.ToLowerInvariant();

        // O host da marca em si, ou um subdomínio de tenant dentro dela.
        var isPlatformHost = _platformDomains.Any(d =>
            host == d || host.EndsWith($".{d}", StringComparison.Ordinal));

        return isPlatformHost ? host : null;
    }
}
