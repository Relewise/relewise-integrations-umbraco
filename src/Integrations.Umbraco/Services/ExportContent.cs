using System;
using Umbraco.Cms.Core.Models;

namespace Relewise.Integrations.Umbraco.Services;

public class ExportContent
{
    private readonly long? _version;

    public ExportContent(IContent[] contents, long? version = null)
    {
        Contents = contents;
        _version = version;
    }

    public IContent[] Contents { get; }

    public long Version => _version ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}