using System;
using System.Collections.Generic;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

public class RelewiseConfiguration
{
    public RelewiseConfiguration(HashSet<string> trackableDocTypes)
    {
        TrackableDocTypes = trackableDocTypes ?? throw new ArgumentNullException(nameof(trackableDocTypes));
    }

    public HashSet<string> TrackableDocTypes { get; }

    public bool IsTrackable(IPublishedElement content)
    {
        return IsTrackable(content.ContentType.Alias);
    }

    public bool IsTrackable(IContent content)
    {
        return IsTrackable(content.ContentType.Alias);
    }

    private bool IsTrackable(string docAlias)
    {
        return TrackableDocTypes.Contains(docAlias);
    }
}