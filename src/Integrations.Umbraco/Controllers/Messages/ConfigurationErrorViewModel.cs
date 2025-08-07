namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>
/// Represents an error in the Relewise configuration
/// </summary>
public class ConfigurationErrorViewModel(string errorMessage)
{
    /// <summary>
    /// Indicates whether the Relewise configuration factory failed
    /// </summary>
    public bool FactoryFailed { get; } = true;

    /// <summary>
    /// Error message describing the issue with the Relewise configuration
    /// </summary>
    public string ErrorMessage { get; } = errorMessage;
}