using System.Threading;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco.Mapping;

/// <summary>
/// Maps application fields on a strongly typed destination within an export operation.
/// </summary>
/// <typeparam name="TEntity">The SDK entity type.</typeparam>
public interface IEntityMapper<TEntity> where TEntity : class
{
    /// <summary>
    /// Populate application fields without changing the resolved entity ID.
    /// </summary>
    Task MapAsync(EntityMappingContext<TEntity> context, CancellationToken cancellationToken);
}
