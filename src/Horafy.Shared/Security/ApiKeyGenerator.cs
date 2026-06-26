using System.Security.Cryptography;
using System.Text;

namespace Horafy.Shared.Security;

/// <summary>
/// Gera e valida chaves de API de integração.
///
/// Formato da chave: <c>htf_live_{id}_{secret}</c> (apenas hex, sem ambiguidade de
/// delimitador). Persistimos o prefixo público <c>htf_live_{id}</c> (lookup/exibição)
/// e o hash SHA-256 (hex) do segredo. A chave em texto puro é mostrada só na criação.
/// </summary>
public static class ApiKeyGenerator
{
    private const string Scheme = "htf";
    private const string Environment = "live";

    /// <summary>Gera uma nova chave. Retorna a chave em texto puro, o prefixo e o hash.</summary>
    public static (string PlainKey, string KeyPrefix, string KeyHash) Generate()
    {
        var id     = ToHex(RandomNumberGenerator.GetBytes(8));   // 16 hex
        var secret = ToHex(RandomNumberGenerator.GetBytes(32));  // 64 hex

        var keyPrefix = $"{Scheme}_{Environment}_{id}";
        var plainKey  = $"{keyPrefix}_{secret}";
        var keyHash   = Hash(secret);

        return (plainKey, keyPrefix, keyHash);
    }

    /// <summary>
    /// Faz o parse de uma chave em texto puro, retornando (prefixo, segredo).
    /// Retorna null se o formato for inválido.
    /// </summary>
    public static (string KeyPrefix, string Secret)? Parse(string? plainKey)
    {
        if (string.IsNullOrWhiteSpace(plainKey)) return null;

        var parts = plainKey.Split('_');
        if (parts.Length != 4) return null;
        if (parts[0] != Scheme || parts[1] != Environment) return null;
        if (parts[2].Length == 0 || parts[3].Length == 0) return null;

        var keyPrefix = $"{Scheme}_{Environment}_{parts[2]}";
        return (keyPrefix, parts[3]);
    }

    /// <summary>Compara, em tempo constante, o hash do segredo informado com o hash armazenado.</summary>
    public static bool Verify(string secret, string keyHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(secret)),
            Encoding.ASCII.GetBytes(keyHash));

    private static string Hash(string secret) =>
        ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string ToHex(byte[] bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();
}
