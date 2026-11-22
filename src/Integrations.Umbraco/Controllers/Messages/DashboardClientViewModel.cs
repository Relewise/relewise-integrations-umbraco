namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>Connection settings for an SDK client used by the Umbraco integration.</summary>
/// <param name="name">The SDK client role.</param>
/// <param name="options">Effective settings without credentials.</param>
public class DashboardClientViewModel(string name, ClientOptionsViewObject options)
{
    /// <summary>The SDK client role.</summary>
    public string Name { get; } = name;

    /// <summary>Effective Dataset, server and request timeout.</summary>
    public ClientOptionsViewObject Options { get; } = options;
}
