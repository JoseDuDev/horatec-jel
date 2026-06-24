using Horafy.Application.Interfaces;

namespace Horafy.Infrastructure.Time;

/// <summary>Relógio real do sistema.</summary>
internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
