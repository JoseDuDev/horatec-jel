namespace Horafy.Infrastructure.Gateways;

public sealed class EvolutionApiOptions
{
    public const string SectionName = "EvolutionApi";
    public string BaseUrl      { get; set; } = string.Empty;
    public string ApiKey       { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
}
