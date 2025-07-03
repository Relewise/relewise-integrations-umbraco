using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>
/// Relewise Package configuration model
/// </summary>
public class ConfigurationViewModel
{
    /// <summary>
    /// Contains all contentType aliases that are being tracked
    /// </summary>
    public IReadOnlyCollection<string> TrackedContentTypes { get; }

    /// <summary>
    /// Contains all contentType aliases that are being exported
    /// </summary>
    public IReadOnlyCollection<string> ExportedContentTypes { get; }

    /// <summary>
    /// Contains all named sets of Relewise client options
    /// </summary>
    public List<NamedOptionsViewObject> Named { get; }

    /// <summary>
    /// Indicates whether the Relewise content middleware is enabled
    /// </summary>
    public bool ContentMiddlewareEnabled { get; }

    /// <summary>
    /// Relewise Package configuration model
    /// </summary>
    public ConfigurationViewModel(IReadOnlyCollection<string> trackedContentTypes, IReadOnlyCollection<string> exportedContentTypes, List<NamedOptionsViewObject> named, bool contentMiddlewareEnabled)
    {
        TrackedContentTypes = trackedContentTypes;
        ExportedContentTypes = exportedContentTypes;
        Named = named;
        ContentMiddlewareEnabled = contentMiddlewareEnabled;
    }
}