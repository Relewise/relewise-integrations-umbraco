using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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

internal class RelewiseContentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RelewiseUmbracoConfiguration _configuration;

    public RelewiseContentMiddleware(RequestDelegate next, RelewiseUmbracoConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;

        IsEnabled = true;
    }

    internal static bool IsEnabled { get; private set; }

    public async Task InvokeAsync(HttpContext context)
    {
        IUmbracoContextFactory umbracoContextFactory = context.RequestServices.GetRequiredService<IUmbracoContextFactory>();

        using (UmbracoContextReference umbracoContextReference = umbracoContextFactory.EnsureUmbracoContext())
        {
            if (IsNotInPreview(umbracoContextReference))
            {
                IPublishedContent? content = umbracoContextReference.UmbracoContext?.PublishedRequest?.PublishedContent;

                if (content != null && EnsureContentAndIsTrackable(content))
                {
                    ITracker? tracker = SafeGetTracker(context);

                    if (tracker != null)
                    {
                        User user = await context.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUser();

                        try
                        {
                            Trackable view = CreateView(content, user);
                            await (view switch
                            {
                                ContentView value => tracker.TrackAsync(value, context.RequestAborted),
                                ProductView value => tracker.TrackAsync(value, context.RequestAborted),
                                BrandView value => tracker.TrackAsync(value, context.RequestAborted),
                                ProductCategoryView value => tracker.TrackAsync(value, context.RequestAborted),
                                ContentCategoryView value => tracker.TrackAsync(value, context.RequestAborted),
                                _ => throw new InvalidOperationException("Unknown entity view type.")
                            });
                        }
                        catch (HttpRequestException ex)
                        {
                            if (ex.StatusCode == HttpStatusCode.NotFound)
                            {
                                throw new InvalidOperationException($"The Dataset Id '{tracker.DatasetId}' is not known by Relewise - You can always find your available dataset id's on https://my.relewise.com", ex);
                            }

                            throw;
                        }
                    }
                }
            }
        }

        await _next.Invoke(context);
    }

    private Trackable CreateView(IPublishedContent content, User user)
    {
        string id = _configuration.GetContentId(content);
        if (!_configuration.Mappings.TryGetValue(content.ContentType.Alias, out MappingDefinition? mapping))
        {
            return new ContentView(user, id);
        }

        if (mapping.EntityType == typeof(ProductVariant))
        {
            IPublishedContent parent = content.Parent() ?? throw new InvalidOperationException("A Variant view requires its mapped Product parent.");
            if (!_configuration.Mappings.TryGetValue(parent.ContentType.Alias, out MappingDefinition? parentMapping)
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

    private string[] CategoryPath(IPublishedContent content, Type entityType)
        => content.Breadcrumbs(andSelf: true)
            .Where(node => _configuration.Mappings.TryGetValue(node.ContentType.Alias, out MappingDefinition? mapping) && mapping.EntityType == entityType)
            .Select(_configuration.GetContentId).ToArray();

    private static ITracker? SafeGetTracker(HttpContext context)
    {
        try
        {
            return context.RequestServices.GetRequiredService<IRelewiseClientFactory>().GetClient<ITracker>(Constants.NamedClientName);
        }
        catch (Exception ex)
        {
            ILogger<RelewiseContentMiddleware>? logger = context.RequestServices.GetService<ILogger<RelewiseContentMiddleware>>();

            logger?.LogError(ex, $"Unable to resolve {nameof(ITracker)}-instance. {{Message}}", ex.Message);

            return null;
        }
    }

    private static bool IsNotInPreview(UmbracoContextReference umbracoContextReference)
    {
        return umbracoContextReference.UmbracoContext?.InPreviewMode == false;
    }

    private bool EnsureContentAndIsTrackable(IPublishedContent content)
    {
        return _configuration.IsTrackable(content);
    }
}
