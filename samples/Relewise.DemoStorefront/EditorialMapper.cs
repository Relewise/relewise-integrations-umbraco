using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;

/// <summary>Runs after declarative mappings and demonstrates application-level enrichment.</summary>
internal sealed class EditorialMapper : IEntityMapper<Content>
{
    public Task MapAsync(EntityMappingContext<Content> context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Existing declaratively mapped values are available and may be extended or replaced.
        context.Entity.Data ??= new();
        context.Entity.Data["searchText"] = string.Join("\n", new[] { "bodyText", "contentRows" }
            .Where(context.Entity.Data.ContainsKey).Select(key => context.Entity.Data[key]?.Value?.ToString())
            .Where(text => !string.IsNullOrWhiteSpace(text)));
        context.Entity.Data["editorialSource"] = "Umbraco demo storefront";
        return Task.CompletedTask;
    }
}
