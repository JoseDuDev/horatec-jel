using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Reviews;

public sealed class Review : BaseEntity
{
    private Review() { }

    public Guid    BookingId  { get; private set; }
    public Guid    ResourceId { get; private set; }
    public Guid    CustomerId { get; private set; }
    public int     Stars      { get; private set; }
    public string? Comment    { get; private set; }

    /// <summary>Resposta pública do estabelecimento à avaliação (opcional).</summary>
    public string?         OwnerReply     { get; private set; }
    public DateTimeOffset? OwnerRepliedAt { get; private set; }

    public static Review Create(
        Guid    bookingId,
        Guid    resourceId,
        Guid    customerId,
        int     stars,
        string? comment = null)
    {
        if (stars < 1 || stars > 5)
            throw new ArgumentOutOfRangeException(nameof(stars), "Avaliação deve ser entre 1 e 5 estrelas.");

        return new Review
        {
            BookingId  = bookingId,
            ResourceId = resourceId,
            CustomerId = customerId,
            Stars      = stars,
            Comment    = comment?.Trim()
        };
    }

    public void Update(int stars, string? comment)
    {
        if (stars < 1 || stars > 5)
            throw new ArgumentOutOfRangeException(nameof(stars), "Avaliação deve ser entre 1 e 5 estrelas.");

        Stars     = stars;
        Comment   = comment?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Registra (ou edita) a resposta pública do estabelecimento.</summary>
    public void Reply(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            throw new ArgumentException("A resposta não pode ser vazia.", nameof(reply));

        OwnerReply     = reply.Trim();
        OwnerRepliedAt = DateTimeOffset.UtcNow;
        UpdatedAt      = DateTimeOffset.UtcNow;
    }
}
