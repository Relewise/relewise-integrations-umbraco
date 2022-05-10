using System;
using System.Collections.Generic;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

public class RelewiseConfiguration
{
    // NOTE: Selvom vi kører med nullable, tænker jeg vi bør teste for null alligevel - eller fletter compileren dette ind?

    public RelewiseConfiguration(Guid datasetId, string apiKey, HashSet<string> trackableDocTypes)
    {
        DatasetId = datasetId;
        ApiKey = apiKey;
        TrackableDocTypes = trackableDocTypes;
    }

    // NOTE: Disse to herunder erstattes vel af dét du laver på Relewise.Client.Extensions projektet?
    public Guid DatasetId { get; }
    public string ApiKey { get; }

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