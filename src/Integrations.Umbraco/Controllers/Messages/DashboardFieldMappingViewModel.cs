namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>Inspectable source, destination and conversion for a declared field mapping.</summary>
public class DashboardFieldMappingViewModel
{
    /// <summary>The source Umbraco property alias.</summary>
    public string SourcePropertyAlias { get; internal set; } = string.Empty;

    /// <summary>The built-in Relewise property or Data destination.</summary>
    public string Destination { get; internal set; } = string.Empty;

    /// <summary>The configured conversion; automatic selection is resolved during export.</summary>
    public string Conversion { get; internal set; } = string.Empty;
}
