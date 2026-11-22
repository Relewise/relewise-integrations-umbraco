using Microsoft.Extensions.Caching.Memory;

namespace Relewise.DemoStorefront.Features.Cart;

internal static class CartStateStore
{
    private const string SessionMarkerKey = "fieldwork-shopping";

    // A session identifies the basket, but its per-request snapshot cannot serialize concurrent writes.
    private static readonly object StateCreation = new();

    internal static CartState Get(HttpContext http)
    {
        // Establish the session for both page renders and clients starting at the JSON endpoint.
        if (http.Session.GetString(SessionMarkerKey) == null)
        {
            http.Session.SetString(SessionMarkerKey, "active");
        }

        var key = "fieldwork-basket-" + http.Session.Id;
        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();

        lock (StateCreation)
        {
            return cache.GetOrCreate(key,
                entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromHours(8);

                    return new CartState();
                })!;
        }
    }

    internal static int Count(HttpContext http)
    {
        return Get(http).Lines.Sum(line => line.Quantity);
    }
}
