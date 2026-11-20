#pragma warning disable CS0618 // Keep built-ins in their original registration order for legacy AutoMap converters.
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Strings;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

/// <summary>
/// Handles extracting published HTML for 'Umbraco.RichText' (Tiptap) and 'Umbraco.TinyMCE'.
/// </summary>
public class RichTextEditorPropertyValueConverter : IDefaultPropertyValueConverter, IRelewisePropertyValueConverter
{
    /// <inheritdoc />
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias is "Umbraco.RichText" or "Umbraco.TinyMCE";
    }

    /// <inheritdoc />
    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Convert(context);
        return default;
    }

    /// <inheritdoc/>
    [System.Obsolete("Use ConvertAsync instead. Synchronous conversion will be removed in the next major version.")]
    public void Convert(RelewisePropertyConverterContext context)
    {
        IHtmlEncodedString? value = context.Property.GetValue<IHtmlEncodedString>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(value?.ToHtmlString()));
    }
}
