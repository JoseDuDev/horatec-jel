using Horafy.Domain.Entities.Rentals;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IRentableItemRepository : IRepository<RentableItem>
{
    Task<IReadOnlyList<RentableItem>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentableItem>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega o item com a galeria e <b>com rastreamento</b> — é por aqui que as
    /// operações de foto entram, já que mexer na coleção exige o EF acompanhando
    /// o agregado para gerar o DELETE das fotos removidas.
    /// </summary>
    Task<RentableItem?> GetByIdWithImagesAsync(Guid id, CancellationToken cancellationToken = default);
}
