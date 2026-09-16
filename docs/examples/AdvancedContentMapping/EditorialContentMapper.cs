using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Services;

namespace Relewise.MappingExamples;

public sealed class EditorialContentMapper : IContentTypeMapping
{
    // These aliases describe the example site's schema. Adapt them to your site.
    private static readonly HashSet<string> StandardFields = new(StringComparer.Ordinal)
    {
        "title", "summary", "tags"
    };

    private static readonly string[] BlockFields = ["contentRows", "pageGrid"];

    public Task<ContentUpdate> Map(ContentMappingContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var converter = context.GetRequiredService<IRelewisePropertyConverter>();
        var cultures = context.CulturesToPublish.ToArray();
        var ordinaryProperties = context.PublishedContent.Properties.Where(property =>
            StandardFields.Contains(property.Alias));
        var data = converter.Convert(ordinaryProperties, cultures)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        string TextFor(string culture) => string.Join("\n", BlockFields
            .Select(alias => context.PublishedContent.GetProperty(alias))
            .Where(property => property is not null)
            .Select(property => EditorialBlockText.Extract(property!.GetValue(culture), culture, token))
            .Where(text => text.Length > 0));

        // Always assign this key, including when all blocks were removed.
        // Read each culture even when the outer property is invariant: nested values may vary.
        data["searchText"] = cultures.Length == 1
            ? new DataValue(TextFor(cultures[0]))
            : new DataValue(new Multilingual(cultures
                .Select(culture => new Multilingual.Value(culture, TextFor(culture))).ToArray()));

        // Mutate and return the update supplied by the integration.
        context.ContentUpdate.Content.Data = data;
        return Task.FromResult(context.ContentUpdate);
    }
}
