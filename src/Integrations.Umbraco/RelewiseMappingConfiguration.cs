using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

//public class RelewiseMappingConfiguration
//{
//    //internal HashSet<string> AutoMappedDocTypes { get; private set; } = new();
//    //internal HashSet<ICustomContentMapping> CustomMappers { get; } = new();
//    //internal HashSet<Type> CustomMappers2 { get; } = new();

//    //public RelewiseMappingConfiguration AutoMapping(params string[] contentTypeAliases)
//    //{
//    //    if (contentTypeAliases == null) throw new ArgumentNullException(nameof(contentTypeAliases));

//    //    AutoMappedDocTypes = contentTypeAliases
//    //        .Where(x => !string.IsNullOrWhiteSpace(x))
//    //        .Distinct(StringComparer.OrdinalIgnoreCase)
//    //        .ToHashSet(StringComparer.OrdinalIgnoreCase);

//    //    return this;
//    //}

//    //public RelewiseMappingConfiguration CustomMapping<T>() where T : class, IContentTypeMapping
//    //{
//    //    T contentMapper = ActivatorUtilities.CreateInstance<T>(new ServiceContainer( ));

//    //    CustomMappers2.Add(typeof(T));
//    //    CustomMappers.Add(contentMapper);
//    //    AutoMappedDocTypes.Add(contentMapper.ContentType);

//    //    return this;
//    //}
//}

public interface IContentTypeMapping
{
    public Task<ContentUpdate> Map(ContentMappingContext context);
}

public class ContentMappingContext : IServiceProvider
{
    private readonly IServiceProvider _serviceProvider;

    public ContentMappingContext(IPublishedContent publishedContent, ContentUpdate contentUpdate,
        List<string> culturesToPublish, IServiceProvider serviceProvider)
    {
        PublishedContent = publishedContent;
        ContentUpdate = contentUpdate;
        CulturesToPublish = culturesToPublish;

        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Content that needs to be mapped
    /// </summary>
    public IPublishedContent PublishedContent { get; }

    /// <summary>
    /// Holds the basic mapping to ensure basic information is handled uniform during the content export
    /// </summary>
    public ContentUpdate ContentUpdate { get; }

    public List<string> CulturesToPublish { get; }

    public object? GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType);
    }
}


