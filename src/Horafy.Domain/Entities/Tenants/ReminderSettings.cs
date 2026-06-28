namespace Horafy.Domain.Entities.Tenants;

/// <summary>
/// Configuração de lembretes automáticos de agendamento por tenant.
/// Define se os lembretes estão ativos e a antecedência (em horas) de cada um.
/// O default preserva o comportamento histórico: lembrete D-1 (24h) e H-2 (2h).
/// </summary>
public sealed class ReminderSettings
{
    private ReminderSettings() { }

    public bool Enabled { get; private set; }

    /// <summary>Antecedência do 1º lembrete, em horas (0 = desativa este lembrete).</summary>
    public int FirstReminderHours { get; private set; }

    /// <summary>Antecedência do 2º lembrete, em horas (0 = desativa este lembrete).</summary>
    public int SecondReminderHours { get; private set; }

    public static readonly ReminderSettings Default =
        new() { Enabled = true, FirstReminderHours = 24, SecondReminderHours = 2 };

    public static ReminderSettings Create(bool enabled, int firstReminderHours, int secondReminderHours)
    {
        if (firstReminderHours is < 0 or > 168)
            throw new ArgumentException("FirstReminderHours deve estar entre 0 e 168.", nameof(firstReminderHours));
        if (secondReminderHours is < 0 or > 168)
            throw new ArgumentException("SecondReminderHours deve estar entre 0 e 168.", nameof(secondReminderHours));
        if (firstReminderHours > 0 && secondReminderHours > 0 && secondReminderHours >= firstReminderHours)
            throw new ArgumentException(
                "O 2º lembrete deve ter antecedência menor que o 1º.", nameof(secondReminderHours));

        return new()
        {
            Enabled             = enabled,
            FirstReminderHours  = firstReminderHours,
            SecondReminderHours = secondReminderHours
        };
    }
}
