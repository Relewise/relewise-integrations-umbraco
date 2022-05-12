using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Services;

internal class RelewiseContentMapper : IContentMapper
{
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly RelewiseMappingConfiguration _mappingConfiguration;
    private readonly IRelewisePropertyConverter _propertyConverter;

    public RelewiseContentMapper(RelewiseMappingConfiguration mappingConfiguration, IUmbracoContextFactory umbracoContextFactory, IRelewisePropertyConverter propertyConverter)
    {
        _mappingConfiguration = mappingConfiguration;
        _umbracoContextFactory = umbracoContextFactory;
        _propertyConverter = propertyConverter;
    }

    public MapContentResult Map(MapContent content)
    {
        if (_mappingConfiguration.AutoMappedDocTypes == null)
            return MapContentResult.Failed;

        if (!_mappingConfiguration.AutoMappedDocTypes.Contains(content.PublishedContent.ContentType.Alias))
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

        var properties = content.PublishedContent.Properties;
        Dictionary<string, DataValue> mappedProperties = _propertyConverter.Convert(properties, culturesToPublish.ToArray());
        mappedProperties.Add(Constants.VersionKey, content.Version);
        mappedProperties.Add("ContentTypeAlias", content.PublishedContent.ContentType.Alias);
        mappedProperties.Add("Url", content.PublishedContent.Url());

        var contentUpdate = new ContentUpdate(new Content(content.PublishedContent.Id.ToString())
        {
            DisplayName = new Multilingual(culturesToPublish.Select(x => new Multilingual.Value(x, content.PublishedContent.Name)).ToList()),
            Data = mappedProperties
        });

        return new MapContentResult(contentUpdate);
    }

    private static string GetDefaultCulture(UmbracoContextReference umbracoContextReference)
    {
        return umbracoContextReference.UmbracoContext.Domains.DefaultCulture.ToLowerInvariant();
    }
}