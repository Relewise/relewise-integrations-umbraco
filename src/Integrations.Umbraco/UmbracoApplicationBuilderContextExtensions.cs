using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Infrastructure.Mvc.Middlewares;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace Relewise.Integrations.Umbraco;

public static class UmbracoApplicationBuilderContextExtensions
{
    /// <summary>
    /// Tracks all page views of registered content types. Requires that an instance of <see cref="IRelewiseUserLocator"/> has been added to the ServiceCollection.
    /// </summary>
    /// <param name="builder"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void TrackContentViews(this IUmbracoApplicationBuilderContext builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        if (builder.ApplicationServices.GetService<IRelewiseUserLocator>() == null)
            throw new InvalidOperationException($"Could not find {nameof(IRelewiseUserLocator)} registered in the DI. Did you remember to add it to the servicecollection? It is required to automatically track page views.");

        builder.AppBuilder.UseMiddleware<RelewiseContentMiddleware>();
    }
}