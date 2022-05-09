using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
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

    public ContentUpdate? Map(IPublishedContent content, long version)
    {
        if (_mappingConfiguration.AutoMappedDocTypes == null)
            return null;

        if (!_mappingConfiguration.AutoMappedDocTypes.Contains(content.ContentType.Alias))
            return null;

        using UmbracoContextReference umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();

        var culturesToPublish = new List<string>();
        if (content.ContentType.VariesByCulture())
        {
            culturesToPublish = content.Cultures.Values.Select(x => x.Culture).ToList();
        }
        else
        {
            culturesToPublish.Add(GetDefaultCulture(umbracoContextReference)); // when not varied by culture the culture info is ""
        }

        var properties = content.Properties;
        Dictionary<string, DataValue> mappedProperties = _propertyConverter.Convert(properties, culturesToPublish.ToArray());
        mappedProperties.Add(Constants.VersionKey, version);
        mappedProperties.Add("ContentTypeAlias", content.ContentType.Alias);
        mappedProperties.Add("Url", content.Url());

        return new ContentUpdate(new Content(content.Id.ToString())
        {
            DisplayName = new Multilingual(culturesToPublish.Select(x => new Multilingual.Value(x, content.Name)).ToList()),
            Data = mappedProperties
        });
    }

    private static string GetDefaultCulture(UmbracoContextReference umbracoContextReference)
    {
        return umbracoContextReference.UmbracoContext.Domains.DefaultCulture.ToLowerInvariant();
    }
}

public struct Constants
{
    internal static readonly string VersionKey = "Relewise_UmbracoVersionId";
}