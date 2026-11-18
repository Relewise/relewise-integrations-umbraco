using System.Threading;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco;

/// <summary>Converts contextual source data to the selected destination property's type.</summary>
/// <typeparam name="TValue">The destination value type, including its nullable contract.</typeparam>
public interface IPropertyMappingConverter<TValue>
{
    /// <summary>Build the complete destination value, including applicable cultures or currencies.</summary>
    ValueTask<TValue> ConvertAsync(PropertyMappingContext context, CancellationToken cancellationToken);
}
