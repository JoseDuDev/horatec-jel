using System.Security.Cryptography;
using System.Text;

namespace Horafy.Application.Features.Auth;

/// <summary>
/// Hash do token de redefinição de senha. O token puro (GUID) vai por e-mail;
/// só o hash SHA-256 (hex) é persistido, já que o token puro nunca deve
/// aparecer em Postgres/backups.
/// </summary>
internal static class PasswordResetTokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
