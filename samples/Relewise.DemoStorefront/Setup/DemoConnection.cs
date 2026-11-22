using System.Text.Json;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core.Search;

namespace Relewise.DemoStorefront.Setup;

// Configuration visibility and upstream error presentation belong to the demo, not the package.
internal static class DemoConnection
{
    internal const string SetupUrl =
        "https://github.com/Relewise/relewise-integrations-umbraco/tree/main/" +
        "samples/Relewise.DemoStorefront#connect-a-local-relewise-dataset";

    internal static bool IsConfigured(HttpContext http)
    {
        return http.RequestServices.GetService<RelewiseUmbracoConfiguration>() != null;
    }

    internal static bool SearchConfigured(HttpContext http)
    {
        return IsConfigured(http) &&
               http.RequestServices
                   .GetRequiredService<
                       ISearcherResolver>()
                   .GetSearcher("catalogue") != null;
    }

    internal static StorefrontResponse Unavailable(HttpContext http, bool json)
    {
        http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        http.Response.Headers.CacheControl = "no-store";

        if (json)
        {
            return new StorefrontResponse(Json: JsonSerializer.Serialize(new { error = UnavailableMessage(http) }));
        }

        return new StorefrontResponse(UnavailablePage(http));
    }

    private static string UnavailableMessage(HttpContext http)
    {
        return Storefront.Text(http, IsConfigured(http) ? "SearchNotEnabled" : "PreviewModeHelp");
    }

    private static StorefrontPage UnavailablePage(HttpContext http)
    {
        string title = Storefront.Text(http, IsConfigured(http) ? "SearchDisabled" : "FeatureUnavailable");

        return new StorefrontPage(
            title,
            "shop",
            "/Views/Shared/Message.cshtml",
            new StorefrontMessage(UnavailableMessage(http), Help: IsConfigured(http)));
    }

    internal static IResult? RequireConnection(HttpContext http, bool json = false)
    {
        if (IsConfigured(http))
        {
            return null;
        }

        http.Response.Headers.CacheControl = "no-store";

        return Unavailable(http, json);
    }

    internal static bool IsConnectionFailure(Exception error, CancellationToken aborted)
    {
        if (aborted.IsCancellationRequested)
        {
            return false;
        }

        return error is HttpRequestException or TimeoutException or OperationCanceledException ||
               (error.InnerException != null && IsConnectionFailure(error.InnerException, aborted));
    }

    internal static async Task InvokeAsync(
        HttpContext http,
        RequestDelegate next)
    {
        try
        {
            await next(http);
        }
        catch (Exception error) when (!http.Response.HasStarted &&
                                      (http.Request.Path.StartsWithSegments("/en") ||
                                       http.Request.Path.StartsWithSegments("/da")) &&
                                      http.RequestServices.GetService<RelewiseUmbracoConfiguration>() != null &&
                                      IsConnectionFailure(error, http.RequestAborted))
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("DemoConnection")
                .LogError(error, "A Relewise request failed in the demo storefront.");
            http.Response.Clear();
            http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            http.Response.Headers.CacheControl = "no-store";

            string message = Storefront.Text(http, "ConnectionUnavailable");

            if (http.Request.Path.Value?.EndsWith("/suggestions", StringComparison.Ordinal) == true ||
                http.Request.Path.Value?.EndsWith("/results", StringComparison.Ordinal) == true)
            {
                await http.Response.WriteAsJsonAsync(
                    new
                    {
                        error = message
                    },
                    http.RequestAborted
                );

                return;
            }

            var page = new StorefrontPage(
                Storefront.Text(http, "ConnectionProblem"),
                "shop",
                "/Views/Shared/Message.cshtml",
                new StorefrontMessage(message, true));
            await new StorefrontResponse(page).ExecuteAsync(http);
        }
    }
}
