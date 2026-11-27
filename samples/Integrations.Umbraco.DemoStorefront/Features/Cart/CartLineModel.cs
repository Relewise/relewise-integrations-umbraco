namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

/// <summary>
/// Display values and canonical identities for one cart line.
/// </summary>
public sealed record CartLineModel
{
    public Guid ProductKey { get; init; }
    public Guid? VariantKey { get; init; }
    public string ProductId { get; init; } = "";
    public string? VariantId { get; init; }
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string ImageHtml { get; init; } = "";
    public int Quantity { get; init; }
    public int Maximum { get; init; }
    public string UnitPrice { get; init; } = "";
    public string LineTotal { get; init; } = "";
    public bool Available { get; init; }
}
