using FluentAssertions;
using Horafy.Application.Features.Notifications;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_ReplacesAllVariables()
    {
        var template = "Olá, {{customer_name}}! Serviço: {{service_name}}.";
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = "João",
            ["service_name"]  = "Corte"
        };
        TemplateRenderer.Render(template, vars).Should().Be("Olá, João! Serviço: Corte.");
    }

    [Fact]
    public void Render_MissingVariable_LeavesPlaceholderIntact()
    {
        var template = "Olá, {{customer_name}}! Serviço: {{service_name}}.";
        var vars = new Dictionary<string, string> { ["customer_name"] = "Maria" };
        var result = TemplateRenderer.Render(template, vars);
        result.Should().Be("Olá, Maria! Serviço: {{service_name}}.");
    }

    [Fact]
    public void Render_EmptyTemplate_ReturnsEmpty()
    {
        TemplateRenderer.Render("", new Dictionary<string, string>()).Should().BeEmpty();
    }

    [Fact]
    public void FormatBrazilianDateTime_FormatsCorrectly()
    {
        var dt = new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.FromHours(-3));
        TemplateRenderer.FormatBrazilian(dt).Should().Be("15/06/2026 14:30");
    }
}
