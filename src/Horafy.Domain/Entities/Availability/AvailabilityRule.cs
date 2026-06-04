using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

public sealed class AvailabilityRule : BaseEntity
{
    private AvailabilityRule() { }

    public Guid      ResourceId          { get; private set; }
    public DayOfWeek DayOfWeek           { get; private set; }
    public TimeOnly  StartTime           { get; private set; }
    public TimeOnly  EndTime             { get; private set; }
    public int       SlotDurationMinutes { get; private set; }
    public int       BreakAfterMinutes   { get; private set; }

    public static AvailabilityRule Create(
        Guid resourceId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes,
        int breakAfterMinutes = 0)
    {
        if (startTime >= endTime)
            throw new ArgumentException("Horário de início deve ser anterior ao fim.");
        if (slotDurationMinutes <= 0)
            throw new ArgumentException("Duração do slot deve ser maior que zero.");
        if (breakAfterMinutes < 0)
            throw new ArgumentException("Intervalo não pode ser negativo.");

        return new AvailabilityRule
        {
            ResourceId          = resourceId,
            DayOfWeek           = dayOfWeek,
            StartTime           = startTime,
            EndTime             = endTime,
            SlotDurationMinutes = slotDurationMinutes,
            BreakAfterMinutes   = breakAfterMinutes
        };
    }

    public void Update(TimeOnly startTime, TimeOnly endTime,
        int slotDurationMinutes, int breakAfterMinutes)
    {
        if (startTime >= endTime)
            throw new ArgumentException("Horário de início deve ser anterior ao fim.");

        StartTime           = startTime;
        EndTime             = endTime;
        SlotDurationMinutes = slotDurationMinutes;
        BreakAfterMinutes   = breakAfterMinutes;
        UpdatedAt           = DateTimeOffset.UtcNow;
    }
}
