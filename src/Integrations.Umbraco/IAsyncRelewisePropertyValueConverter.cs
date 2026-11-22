using System.Threading;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Converts an Umbraco property into one or more Relewise Data values.
/// </summary>
public interface IAsyncRelewisePropertyValueConverter
{
    /// <summary>
    /// Whether this converter handles the contextual source property.
    /// </summary>
    bool CanHandle(RelewisePropertyConverterContext context);

    /// <summary>
    /// Populate Data values, with access to the owning document and operation services.
    /// </summary>
    ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken);
}
