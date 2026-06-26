using System.Security.Cryptography;
using System.Text;

namespace Horafy.Shared.Security;

/// <summary>
/// Assinatura HMAC-SHA256 dos webhooks de saída. O receptor (ex.: Atendefy) recomputa
/// a assinatura sobre o corpo bruto com o mesmo segredo e compara em tempo constante.
/// </summary>
public static class WebhookSignature
{
    /// <summary>Gera um segredo aleatório (hex, 64 chars).</summary>
    public static string NewSecret() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    /// <summary>Calcula a assinatura no formato <c>sha256=&lt;hex&gt;</c>.</summary>
    public static string Compute(string secret, string payload)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Compara duas assinaturas em tempo constante.</summary>
    public static bool Verify(string secret, string payload, string signature) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Compute(secret, payload)),
            Encoding.ASCII.GetBytes(signature ?? string.Empty));
}
