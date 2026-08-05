using Horafy.Domain.Entities.Leads;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IPlatformLeadRepository : IRepository<PlatformLead>
{
    /// <summary>Existe lead com este telefone criado após o instante dado? (anti-duplicata)</summary>
    Task<bool> RecentPhoneExistsAsync(string phone, DateTimeOffset since, CancellationToken cancellationToken = default);
}
