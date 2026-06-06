namespace Horafy.Application.Interfaces;

public interface IWhatsAppService
{
    Task SendTextAsync(string phoneNumber, string message, CancellationToken ct = default);
}
