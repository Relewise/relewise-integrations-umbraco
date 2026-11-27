using Relewise.Client.DataTypes;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Integration.Mapping;

/// <summary>
/// Runs after declarative mappings and demonstrates application-level enrichment.
/// </summary>
internal sealed class EditorialMapper(IVariationContextAccessor variations) : IEntityMapper<Content>
{
    private static readonly string[] BlockFields = [PropertyAliases.ContentRows, PropertyAliases.PageGrid];

    public Task MapAsync(EntityMappingContext<Content> context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        context.Entity.Data ??= new Dictionary<string, DataValue?>();

        string ConvertedText(string key, string culture)
        {
            if (!context.Entity.Data.TryGetValue(key, out DataValue? value))
            {
                return string.Empty;
            }

            return value?.Value switch
            {
                string text => text,
                Multilingual multilingual => multilingual.Values?
                    .FirstOrDefault(localized => string.Equals(
                        localized.Language.Value,
                        culture,
                        StringComparison.OrdinalIgnoreCase
                    ))
                    ?.Text ?? string.Empty,
                _ => string.Empty
            };
        }

        string BlockText(string alias, string culture)
        {
            IPublishedProperty? property = context.PublishedContent.GetProperty(alias);

            // An invariant outer block property can contain culture-varying elements.
            // Read those elements per culture rather than reusing the first culture's converted text.
            if (property != null &&
                ((!property.PropertyType.VariesByCulture() && context.CulturesToPublish.Count > 1) ||
                 !context.Entity.Data.ContainsKey(alias)))
            {
                return EditorialBlockText.ExtractProperty(
                    property,
                    culture,
                    variations,
                    token
                );
            }

            return ConvertedText(alias, culture);
        }

        string TextFor(string culture)
        {
            token.ThrowIfCancellationRequested();

            return string.Join(
                "\n",
                new[] { ConvertedText(DataKeys.BodyText, culture) }
                    .Concat(BlockFields.Select(alias => BlockText(alias, culture)))
                    .Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        // Always overwrite the field, including when all relevant text was removed.
        context.Entity.Data[DataKeys.SearchText] = context.CulturesToPublish.Count == 1
            ? new DataValue(TextFor(context.CulturesToPublish[0]))
            : new DataValue(
                new Multilingual(
                    context.CulturesToPublish
                        .Select(culture => new Multilingual.Value(culture, TextFor(culture)))
                        .ToArray()));
        context.Entity.Data[DataKeys.EditorialSource] = "Umbraco demo storefront";

        return Task.CompletedTask;
    }
}
