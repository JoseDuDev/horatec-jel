namespace Horafy.Application.Interfaces;

/// <summary>
/// Resolve o host que deve aparecer nos links que a aplicação envia por e-mail.
///
/// O mesmo backend serve duas marcas (AGENDA e ALUGUE, ver frontend/lib/brand.ts),
/// então um host fixo em configuração manda o administrador de uma locadora para a
/// marca errada. O host sai da requisição que originou o e-mail.
/// </summary>
public interface IPlatformUrlService
{
    /// <summary>
    /// Monta uma URL absoluta para <paramref name="path"/> no host da marca de onde
    /// veio a requisição. Cai para `Platform:Domain` quando não dá para confiar na
    /// origem (chamada de fora do browser, job em background, origem desconhecida).
    /// </summary>
    string BuildUrl(string path);
}
