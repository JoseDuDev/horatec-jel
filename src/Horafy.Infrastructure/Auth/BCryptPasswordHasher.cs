using Horafy.Application.Interfaces;

namespace Horafy.Infrastructure.Auth;

internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    // WorkFactor 12 é o padrão recomendado: seguro sem ser lento demais
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
