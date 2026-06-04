using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

public sealed class BusinessHours : BaseEntity
{
    private BusinessHours() { }

    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly  OpenTime  { get; private set; }
    public TimeOnly  CloseTime { get; private set; }
    public bool      IsOpen    { get; private set; }

    public static BusinessHours Create(DayOfWeek day, TimeOnly open, TimeOnly close, bool isOpen = true)
    {
        if (isOpen && open >= close)
            throw new ArgumentException("Horário de abertura deve ser anterior ao de fechamento.");

        return new BusinessHours
        {
            DayOfWeek = day,
            OpenTime  = open,
            CloseTime = close,
            IsOpen    = isOpen
        };
    }

    public void Update(TimeOnly open, TimeOnly close, bool isOpen)
    {
        if (isOpen && open >= close)
            throw new ArgumentException("Horário de abertura deve ser anterior ao de fechamento.");

        OpenTime  = open;
        CloseTime = close;
        IsOpen    = isOpen;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
