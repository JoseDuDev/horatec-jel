using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Availability;

public sealed class Holiday : BaseEntity
{
    private Holiday() { }

    public string  Name                { get; private set; } = string.Empty;
    public DateOnly Date               { get; private set; }
    public bool    IsRecurringAnnually { get; private set; }
    public string? Reason              { get; private set; }

    public static Holiday Create(string name, DateOnly date, bool isRecurringAnnually, string? reason = null) =>
        new()
        {
            Name                = name.Trim(),
            Date                = date,
            IsRecurringAnnually = isRecurringAnnually,
            Reason              = reason?.Trim()
        };

    public void Update(string name, DateOnly date, bool isRecurringAnnually, string? reason)
    {
        Name                = name.Trim();
        Date                = date;
        IsRecurringAnnually = isRecurringAnnually;
        Reason              = reason?.Trim();
    }
}
