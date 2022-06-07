using System;
using Umbraco.Cms.Core.Models;

namespace Relewise.Integrations.Umbraco.Services;

/// <summary>
///  Holds the request for a content export
/// </summary>
public class ExportContent
{
    private readonly long? _version;

#pragma warning disable 1591
    public ExportContent(IContent[] contents, long? version = null)
#pragma warning restore 1591
    {
        Contents = contents;
        _version = version;
    }

    /// <summary>
    /// Umbraco content to export
    /// </summary>
    public IContent[] Contents { get; }

    /// <summary>
    /// Feed version for enabling and disabling content
    /// </summary>
    public long Version => _version ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}