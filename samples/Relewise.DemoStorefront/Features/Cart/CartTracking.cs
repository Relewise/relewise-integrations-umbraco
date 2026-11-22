using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests;
using Relewise.DemoStorefront.Integration;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Services;
using RelewiseCart = Relewise.Client.DataTypes.Cart;

namespace Relewise.DemoStorefront.Features.Cart;

internal static class CartTracking
{
    internal static async Task TrackCart(PricedCartLine[] lines, HttpContext http)
    {
        ITracker tracker = RequiredTracker(http);
        User user = await http.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUserAsync();

        await tracker.TrackAsync(
            new RelewiseCart(
                user,
                new Money(
                    DemoMarket.Current(http).Currency,
                    lines.Sum(line => line.Amount * line.Line.Quantity)),
                Items(lines, http)),
            http.RequestAborted);
    }

    private static ITracker RequiredTracker(HttpContext http)
    {
        return http.RequestServices.GetRequiredService<IExportContentService>().GetTrackerOrNull()
               ?? throw new InvalidOperationException(
                   "The demo requires a configured Relewise Tracker for shopping actions.");
    }

    private static LineItem[] Items(PricedCartLine[] lines, HttpContext http)
    {
        return
        [
            .. lines.Select(line => new LineItem(
                DemoConfiguration.EntityId(line.Product, http.RequestServices),
                line.Variant == null ? null : DemoConfiguration.EntityId(line.Variant, http.RequestServices),
                line.Line.Quantity,
                line.Amount * line.Line.Quantity
            ))
        ];
    }

    internal static async Task TrackOrderAsync(PricedCartLine[] lines, string orderNumber, HttpContext http)
    {
        ITracker tracker = RequiredTracker(http);
        User user = await http.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUserAsync();
        string currency = DemoMarket.Current(http).Currency;
        decimal total = lines.Sum(line => line.Amount * line.Line.Quantity);
        Trackable[] events =
        [
            new Order(user, new Money(currency, total), orderNumber, Items(lines, http)),
            new RelewiseCart(user, new Money(currency, 0m), Array.Empty<LineItem>())
        ];

        await tracker.TrackAsync(events, http.RequestAborted);
    }
}
