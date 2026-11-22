using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Features.Cart;

internal sealed record PricedCartLine(
    CartLine Line,
    IPublishedContent Product,
    IPublishedContent? Variant,
    decimal Amount);
