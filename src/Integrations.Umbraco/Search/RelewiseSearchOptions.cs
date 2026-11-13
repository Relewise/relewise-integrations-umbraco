using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco.Search;

/// <summary>Configures the opt-in Umbraco core search provider.</summary>
public sealed class RelewiseSearchOptions
{
    /// <summary>A stable, unique site/environment identifier. Required; isolates indexing and deletion.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>The configured Relewise client. Prefer a dedicated Dataset for core search.</summary>
    public string ClientName { get; set; } = Constants.NamedClientName;

    /// <summary>The Dataset search language used for the provider's text. Culture selection is independent.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Indexes to replace. Published content is the default; draft/media/member/element indexes are opt-in.</summary>
    public ISet<string> IndexAliases { get; } = new HashSet<string>
    {
        global::Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent
    };
}
