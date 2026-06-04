using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

public sealed class AvailabilityException : BaseEntity
{
    private AvailabilityException() { }

    public Guid      ResourceId  { get; private set; }
    public DateOnly  Date        { get; private set; }
    public bool      IsBlocked   { get; private set; }
    public TimeOnly? CustomStart { get; private set; }
    public TimeOnly? CustomEnd   { get; private set; }
    public string?   Reason      { get; private set; }

    public static AvailabilityException CreateBlock(Guid resourceId, DateOnly date, string? reason = null) =>
        new()
        {
            ResourceId = resourceId,
            Date       = date,
            IsBlocked  = true,
            Reason     = reason?.Trim()
        };

    public static AvailabilityException CreateCustomHours(
        Guid resourceId, DateOnly date, TimeOnly customStart, TimeOnly customEnd, string? reason = null)
    {
        if (customStart >= customEnd)
            throw new ArgumentException("Horário de início deve ser anterior ao fim.");

        return new AvailabilityException
        {
            ResourceId  = resourceId,
            Date        = date,
            IsBlocked   = false,
            CustomStart = customStart,
            CustomEnd   = customEnd,
            Reason      = reason?.Trim()
        };
    }
}
