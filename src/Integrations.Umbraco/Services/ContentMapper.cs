using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Services;

internal class ContentMapper : IContentMapper
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly RelewiseUmbracoConfiguration _configuration;
    private readonly IRelewisePropertyConverter _propertyConverter;
    private readonly IServiceProvider _provider;

    public ContentMapper(RelewiseUmbracoConfiguration configuration, IUmbracoContextFactory umbracoContextFactory, IRelewisePropertyConverter propertyConverter, IServiceProvider provider)
    {
        _configuration = configuration;
        _umbracoContextFactory = umbracoContextFactory;
        _propertyConverter = propertyConverter;
        _provider = provider;
    }

    public MapContentResult Map(MapContent content)
    {
        if (!_configuration.CanMap(content.PublishedContent.ContentType.Alias))
            return MapContentResult.Failed;

        using UmbracoContextReference umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();

        var culturesToPublish = new List<string>();
        if (content.PublishedContent.ContentType.VariesByCulture())
        {
            culturesToPublish = content.PublishedContent.Cultures.Values.Select(x => x.Culture).ToList();
        }
        else
        {
            culturesToPublish.Add(GetDefaultCulture(umbracoContextReference)); // when not varied by culture the culture info is ""
        }

        var contentUpdate = new ContentUpdate(new Content(content.PublishedContent.Id.ToString())
        {
            DisplayName = MapDisplayName(content.PublishedContent, culturesToPublish),
            CategoryPaths = GetCategoryPaths(content, culturesToPublish)
        });

        AutoMapOrUseMapper(content, culturesToPublish, contentUpdate);

        return new MapContentResult(contentUpdate);
    }

    private void AutoMapOrUseMapper(MapContent content, List<string> culturesToPublish, ContentUpdate contentUpdate)
    {
        if (TryGetMapper(content, out IContentTypeMapping? mapping))
        {
            mapping.Map(new ContentMappingContext(content.PublishedContent, contentUpdate, culturesToPublish, _provider));
        }
        else
        {
            contentUpdate.Content.Data = _propertyConverter.Convert(content.PublishedContent.Properties, culturesToPublish.ToArray());
        }

        contentUpdate.Content.Data.Add(Constants.VersionKey, content.Version);
        contentUpdate.Content.Data.Add("ContentTypeAlias", content.PublishedContent.ContentType.Alias);
        contentUpdate.Content.Data.Add("Url", content.PublishedContent.Url());
        contentUpdate.Content.Data.Add("CreatedAt", new DateTimeOffset(content.PublishedContent.CreateDate).ToUnixTimeSeconds());

        bool TryGetMapper(MapContent mapContent, [NotNullWhen(true) ]out IContentTypeMapping? contentTypeMapping)
        {
            return _configuration.TryGetMapper(mapContent.PublishedContent.ContentType.Alias, out contentTypeMapping);
        }
    }

    private static List<CategoryPath>? GetCategoryPaths(MapContent content, List<string> culturesToPublish)
    {
        if (content.PublishedContent.Parent == null)
            return null;

        return new List<CategoryPath>
        {
            new(content.PublishedContent
                .Breadcrumbs(andSelf: false)
                .Select(x => new CategoryNameAndId(x.Id.ToString(), MapDisplayName(x, culturesToPublish)))
                .ToArray())
        };
    }

    private static Multilingual MapDisplayName(IPublishedContent content, List<string> culturesToPublish)
    {
        return new Multilingual(culturesToPublish.Select(x => new Multilingual.Value(x, content.Name)).ToList());
    }

    private static string GetDefaultCulture(UmbracoContextReference umbracoContextReference)
    {
        return umbracoContextReference.UmbracoContext.Domains.DefaultCulture.ToLowerInvariant();
    }
}