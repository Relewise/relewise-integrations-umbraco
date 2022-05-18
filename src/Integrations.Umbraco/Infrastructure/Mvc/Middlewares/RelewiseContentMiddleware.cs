using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Relewise.Integrations.Umbraco.Infrastructure.Mvc.Middlewares;

internal class RelewiseContentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RelewiseConfiguration _configuration;
    private readonly IRelewiseUserLocator _userLocator;

    public RelewiseContentMiddleware(RequestDelegate next, RelewiseConfiguration configuration, IRelewiseUserLocator userLocator)
    {
        _next = next;
        _configuration = configuration;
        _userLocator = userLocator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        IUmbracoContextFactory umbracoContextFactory = context.RequestServices.GetRequiredService<IUmbracoContextFactory>();

        using (UmbracoContextReference umbracoContextReference = umbracoContextFactory.EnsureUmbracoContext())
        {
            IPublishedContent? content = umbracoContextReference.UmbracoContext?.PublishedRequest?.PublishedContent;

            if (IsNotInPreview(umbracoContextReference) && EnsureContentAndIsTrackable(content))
            {
                ITracker tracker = context.RequestServices.GetRequiredService<ITracker>();

                User user = await _userLocator.GetUser();
                try
                {
                    await tracker.TrackAsync(new ContentView(user, content?.Id.ToString()));
                }
                catch (HttpRequestException e)
                {
                    if (e.StatusCode == HttpStatusCode.NotFound)
                        throw new InvalidOperationException("Could not find dataset id in Relewise");

                    throw;
                }
            }
        }

        await _next.Invoke(context);
    }

    private static bool IsNotInPreview(UmbracoContextReference umbracoContextReference)
    {
        return umbracoContextReference.UmbracoContext?.InPreviewMode == false;
    }

    private bool EnsureContentAndIsTrackable(IPublishedContent? content)
    {
        return content != null && _configuration.IsTrackable(content);
    }
}