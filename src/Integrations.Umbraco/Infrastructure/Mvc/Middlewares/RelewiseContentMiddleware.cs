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

    public RelewiseContentMiddleware(RequestDelegate next, RelewiseConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
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

                // NOTE: Her skal vi selvfølgelig have ét eller andet ind, som - uanset hvor vi er - kan vi tilgå en User
                //  -> det kunne evt. være noget Middleware
                //  -> men i hvert fald skal det være noget, som kan klistre instansen på HttpContext

                await tracker.TrackAsync(new ContentView(User.Anonymous(), content?.Id.ToString()));
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