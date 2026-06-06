namespace Horafy.Application.Features.Notifications;

public static class TemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        return result;
    }

    public static string FormatBrazilian(DateTimeOffset dt) =>
        dt.ToString("dd/MM/yyyy HH:mm");
}
