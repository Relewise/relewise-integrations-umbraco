using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Infrastructure.Mvc.Middlewares;

internal class RelewiseViewTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RelewiseUmbracoConfiguration _configuration;

    public RelewiseViewTrackingMiddleware(RequestDelegate next, RelewiseUmbracoConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;

        IsEnabled = true;
    }

    internal static bool IsEnabled { get; private set; }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            var contexts = context.RequestServices.GetRequiredService<IUmbracoContextFactory>();

            using UmbracoContextReference reference = contexts.EnsureUmbracoContext();
            IPublishedContent? content = reference.UmbracoContext.PublishedRequest?.PublishedContent;

            if (IsNotInPreview(reference) && content != null && EnsureContentAndIsTrackable(content))
            {
                ITracker? tracker = SafeGetTracker(context);

                if (tracker != null)
                {
                    User user = await context.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUserAsync();
                    Trackable view = CreateView(content, user);
                    var logger = context.RequestServices.GetService<ILogger<RelewiseViewTrackingMiddleware>>();

                    // Snapshot the entity/user while the Umbraco context is alive. OnCompleted runs
                    // after page delivery and before request-service disposal; no detached task is started.
                    context.Response.OnCompleted(() => TrackViewAsync(context, tracker, view, logger));
                }
            }
        }

        await _next.Invoke(context);
    }

    private static async Task TrackViewAsync(
        HttpContext context,
        ITracker tracker,
        Trackable view,
        ILogger<RelewiseViewTrackingMiddleware>? logger)
    {
        if (context.Response.StatusCode is < 200 or >= 300 || context.RequestAborted.IsCancellationRequested ||
            context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await (view switch
            {
                ContentView value => tracker.TrackAsync(value, timeout.Token),
                ProductView value => tracker.TrackAsync(value, timeout.Token),
                BrandView value => tracker.TrackAsync(value, timeout.Token),
                ProductCategoryView value => tracker.TrackAsync(value, timeout.Token),
                ContentCategoryView value => tracker.TrackAsync(value, timeout.Token),
                _ => throw new InvalidOperationException("Unknown entity view type.")
            });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The visitor disconnected; this optional event is not retried.
        }
        catch (Exception error) when (error is HttpRequestException or TimeoutException or OperationCanceledException)
        {
            logger?.LogWarning(error, "Relewise entity view tracking failed.");
        }
    }

    private Trackable CreateView(
        IPublishedContent content,
        User user)
    {
        string id = _configuration.GetContentId(content);

        if (!_configuration.Mappings.TryGetMapping(content.ContentType.Alias, out MappingDefinition? mapping))
        {
            return new ContentView(user, id);
        }

        if (mapping.EntityType == typeof(ProductVariant))
        {
            IPublishedContent parent = content.Parent() ??
                                       throw new InvalidOperationException(
                                           "A Variant view requires its mapped Product parent.");

            if (!_configuration.Mappings.TryGetMapping(parent.ContentType.Alias, out MappingDefinition? parentMapping)
                || parentMapping.EntityType != typeof(Product) || parentMapping.Variant != mapping)
            {
                throw new InvalidOperationException("A Variant view requires its configured direct Product parent.");
            }

            return new ProductView(user, new Product(_configuration.GetContentId(parent)), new ProductVariant(id));
        }

        if (mapping.EntityType == typeof(Product))
        {
            return new ProductView(user, new Product(id));
        }

        if (mapping.EntityType == typeof(Brand))
        {
            return new BrandView(user, new Brand(id));
        }

        if (mapping.EntityType == typeof(ProductCategory))
        {
            return new ProductCategoryView(user, CategoryPath(content, typeof(ProductCategory)));
        }

        if (mapping.EntityType == typeof(ContentCategory))
        {
            return new ContentCategoryView(user, CategoryPath(content, typeof(ContentCategory)));
        }

        return new ContentView(user, id);
    }

    private string[] CategoryPath(
        IPublishedContent content,
        Type entityType)
    {
        return
        [
            .. content.Breadcrumbs()
                .Where(node =>
                    _configuration.Mappings.TryGetMapping(node.ContentType.Alias, out MappingDefinition? mapping) &&
                    mapping.EntityType == entityType)
                .Select(_configuration.GetContentId)
        ];
    }

    private static ITracker? SafeGetTracker(HttpContext context)
    {
        try
        {
            return context.RequestServices.GetRequiredService<IRelewiseClientFactory>()
                .GetClient<ITracker>(Constants.ClientNames.Integration);
        }
        catch (Exception ex)
        {
            var logger =
                context.RequestServices.GetService<ILogger<RelewiseViewTrackingMiddleware>>();

            logger?.LogError(ex, $"Unable to resolve {nameof(ITracker)}-instance. {{Message}}", ex.Message);

            return null;
        }
    }

    private static bool IsNotInPreview(UmbracoContextReference umbracoContextReference)
    {
        return !umbracoContextReference.UmbracoContext.InPreviewMode;
    }

    private bool EnsureContentAndIsTrackable(IPublishedContent content)
    {
        return _configuration.IsTrackable(content);
    }
}
