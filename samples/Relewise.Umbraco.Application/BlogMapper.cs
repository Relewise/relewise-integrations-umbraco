using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Umbraco.Application;

public class BlogMapper : IContentTypeMapping
{
    public Task<ContentUpdate> Map(ContentMappingContext context, CancellationToken token)
    {
        context.ContentUpdate.Content.Data["Title"] = context.PublishedContent.GetProperty("title")?.GetValue<string>(context.CulturesToPublish.First());

        return Task.FromResult(context.ContentUpdate);
    }
}