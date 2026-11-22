using Relewise.Client.DataTypes;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.DemoStorefront.Integration.Mapping;

// Sale filters and facets bind directly to these SDK Product Data values.
internal sealed class DemoProductMapper : IEntityMapper<Product>
{
    public Task MapAsync(EntityMappingContext<Product> context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Apply(context.Entity, context.PublishedContent.Value<int>("stock"));
        return Task.CompletedTask;
    }

    internal static void Apply(Product product, int stock)
    {
        product.Data ??= new();
        product.Data["inStock"] = stock > 0;
        Dictionary<string, decimal> sales = Amounts(product.SalesPrice);
        Dictionary<string, decimal> list = Amounts(product.ListPrice);
        foreach (string currency in DemoMarket.All.Select(market => market.Currency).Concat(sales.Keys).Concat(list.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            product.Data["onSale_" + currency] = sales.TryGetValue(currency, out decimal amount) &&
                list.TryGetValue(currency, out decimal original) && amount < original;
        }
    }

    private static Dictionary<string, decimal> Amounts(MultiCurrency? prices) =>
        (prices?.Values ?? []).ToDictionary(price => price.Currency.Value.ToUpperInvariant(), price => price.Amount, StringComparer.OrdinalIgnoreCase);
}
