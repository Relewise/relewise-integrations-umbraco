#pragma warning disable CS0618 // Demonstrates compatibility with existing Content mappers.

using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Services;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Integration.Mapping;

internal sealed class LegacyGuideMapper : IContentTypeMapping
{
    public Task<ContentUpdate> Map(
        ContentMappingContext context,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (context.ContentUpdate.Content.CategoryPaths?.Count != 1)
        {
            throw new InvalidOperationException("Legacy mappers must receive their pre-populated category paths.");
        }

        context.ContentUpdate.Content.CategoryPaths = [];
        context.ContentUpdate.Content.Data = context.GetRequiredService<IRelewisePropertyConverter>()
            .Convert(context.PublishedContent.Properties, [.. context.CulturesToPublish])
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return Task.FromResult(context.ContentUpdate);
    }
}
