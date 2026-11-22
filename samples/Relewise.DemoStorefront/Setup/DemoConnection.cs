using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;

namespace Relewise.DemoStorefront.Setup;

// Configuration visibility and upstream error presentation belong to the demo, not the package.
internal static class DemoConnection
{
    internal const string SetupUrl =
        "https://github.com/Relewise/relewise-integrations-umbraco/tree/main/samples/Relewise.DemoStorefront#connect-a-local-relewise-dataset";

    internal static bool IsConfigured(HttpContext http)
    {
        return http.RequestServices.GetService<RelewiseUmbracoConfiguration>() != null;
    }

    internal static bool SearchConfigured(HttpContext http)
    {
        return IsConfigured(http) &&
               http.RequestServices
                   .GetRequiredService<
                       Umbraco.Cms.Core.Search.ISearcherResolver>()
                   .GetSearcher("catalogue") != null;
    }

    internal static string PreviewNotice(HttpContext http)
    {
        return
            $"<section class='notice demo-preview-notice' role='note'><strong>{Storefront.E(Storefront.Text(http, "PreviewMode"))}</strong><p>{Storefront.E(Storefront.Text(http, "PreviewModeHelp"))}</p><a class='text-link' href='{SetupUrl}'>{Storefront.E(Storefront.Text(http, "ConfigureRelewise"))}</a></section>";
    }

    internal static string Unavailable(HttpContext http,
        bool json)
    {
        http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        http.Response.Headers.CacheControl = "no-store";

        var message = Storefront.Text(http, IsConfigured(http) ? "SearchNotEnabled" : "PreviewModeHelp");

        if (json)
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                error = message
            });
        }

        var title = Storefront.Text(http, IsConfigured(http) ? "SearchDisabled" : "FeatureUnavailable");
        var body = $"<h1>{Storefront.E(title)}</h1>";

        if (IsConfigured(http))
        {
            body +=
                $"<p>{Storefront.E(message)}</p><a href='{SetupUrl}'>{Storefront.E(Storefront.Text(http, "ConfigureRelewise"))}</a>";
        }

        return Storefront.Layout(title, body, false, http, "shop");
    }

    internal static IResult? RequireConnection(HttpContext http, bool json = false)
    {
        if (IsConfigured(http))
        {
            return null;
        }

        var body = Unavailable(http, json);

        return Results.Content(
            body,
            json ? "application/json" : "text/html; charset=utf-8",
            statusCode: StatusCodes.Status503ServiceUnavailable
        );
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

    internal static async Task InvokeAsync(HttpContext http,
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

            var message = Storefront.Text(http, "ConnectionUnavailable");

            if (http.Request.Path.Value?.EndsWith("/suggestions", StringComparison.Ordinal) == true ||
                http.Request.Path.Value?.EndsWith("/results", StringComparison.Ordinal) == true)
            {
                await http.Response.WriteAsJsonAsync(new
                {
                    error = message
                },
                    http.RequestAborted
                );

                return;
            }

            var body =
                $"<section class='notice notice-error' role='alert'><h1>{Storefront.E(Storefront.Text(http, "ConnectionProblem"))}</h1><p>{Storefront.E(message)}</p><a href='{SetupUrl}'>{Storefront.E(Storefront.Text(http, "ConfigureRelewise"))}</a></section>";

            http.Response.ContentType = "text/html; charset=utf-8";
            await http.Response.WriteAsync(Storefront.Layout(
                    Storefront.Text(http, "ConnectionProblem"),
                    body,
                    true,
                    http,
                    "shop"
                ),
                http.RequestAborted
            );
        }
    }
}
