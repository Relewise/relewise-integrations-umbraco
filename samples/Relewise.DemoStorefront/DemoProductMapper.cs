using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Umbraco.Extensions;

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
        var sales = Amounts(product.SalesPrice);
        var list = Amounts(product.ListPrice);
        foreach (string currency in DemoMarket.All.Select(market => market.Currency).Concat(sales.Keys).Concat(list.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            product.Data["onSale_" + currency] = sales.TryGetValue(currency, out decimal amount) &&
                list.TryGetValue(currency, out decimal original) && amount < original;
        }
    }

    private static Dictionary<string, decimal> Amounts(MultiCurrency? prices) =>
        (prices?.Values ?? []).ToDictionary(price => price.Currency.Value.ToUpperInvariant(), price => price.Amount, StringComparer.OrdinalIgnoreCase);
}
