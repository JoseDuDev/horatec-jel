using Horafy.Domain.Entities.Users;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);
    Task<User?> GetByAppleIdAsync(string appleId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Usuários de um tenant cujo telefone (dígitos) bate com alguma das variantes
    /// informadas (ex.: com e sem o prefixo 55). Usado no login por celular.
    /// </summary>
    Task<IReadOnlyList<User>> GetByPhoneAsync(
        IReadOnlyCollection<string> phoneCandidates,
        Guid tenantId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
