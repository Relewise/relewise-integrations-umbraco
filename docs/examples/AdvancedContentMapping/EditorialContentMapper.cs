using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;

namespace Relewise.MappingExamples;

/// <summary>Enriches declaratively mapped Content with selected editorial block text.</summary>
public sealed class EditorialContentMapper : IEntityMapper<Content>
{
    private static readonly string[] BlockFields = ["contentRows", "pageGrid"];

    /// <summary>Build search text after declarative fields, preserving the entity and its mapped values.</summary>
    public Task MapAsync(EntityMappingContext<Content> context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string TextFor(string culture) => string.Join("\n", BlockFields
            .Select(alias => context.PublishedContent.GetProperty(alias))
            .Where(property => property is not null)
            .Select(property => EditorialBlockText.Extract(
                property!.GetValue(property.PropertyType.VariesByCulture() ? culture : null),
                culture,
                cancellationToken))
            .Where(text => text.Length > 0));

        // Read each culture even for invariant outer properties: nested block values may vary.
        string[] cultures = context.CulturesToPublish.ToArray();
        context.Entity.Data ??= new();
        // Always assign the key, including when an editor removes every block.
        context.Entity.Data["searchText"] = cultures.Length == 1
            ? new DataValue(TextFor(cultures[0]))
            : new DataValue(new Multilingual(cultures
                .Select(culture => new Multilingual.Value(culture, TextFor(culture))).ToArray()));

        return Task.CompletedTask;
    }
}
