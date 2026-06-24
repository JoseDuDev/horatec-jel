namespace Horafy.Application.Interfaces;

/// <summary>
/// Abstração do relógio do sistema — permite tempo determinístico em testes
/// (evita dependência de <c>DateTimeOffset.UtcNow</c> direto nos handlers).
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
