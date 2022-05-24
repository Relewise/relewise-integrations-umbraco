using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;

namespace UmbracoV9.Application.Api;

public static class NewsletterApi
{
    public static IEndpointRouteBuilder MapNewsletterRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/newsletter/subscribe", Subscribe);

        return builder;
    }

    private static async Task Subscribe(HttpContext context)
    {
        ITracker tracker = context.RequestServices.GetRequiredService<ITracker>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        user.Email = context.Request.Query["emailAddress"].First();

        await tracker.TrackAsync(new UserUpdate(user), context.RequestAborted);
    }
}