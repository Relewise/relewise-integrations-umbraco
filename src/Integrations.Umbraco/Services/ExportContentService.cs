using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Language = Relewise.Client.DataTypes.Language;

namespace Relewise.Integrations.Umbraco.Services;

public class ExportContentService : IExportContentService
{
    private readonly IContentMapper _contentMapper;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly ITracker _tracker;
    private readonly IContentService _contentService;

    public ExportContentService(IContentMapper contentMapper, IUmbracoContextFactory umbracoContextFactory, ITracker tracker, IContentService contentService)
    {
        _contentMapper = contentMapper;
        _umbracoContextFactory = umbracoContextFactory;
        _tracker = tracker;
        _contentService = contentService;
    }

    public async Task Export(IContent[] contents, long? version = null)
    {
        if (contents.Length == 0)
            return;

        using UmbracoContextReference umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();

        IPublishedContentCache contentCache = umbracoContextReference.UmbracoContext.Content;

        version ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ContentUpdate[] contentUpdates = contents
            .Select(x => _contentMapper.Map(contentCache.GetById(x.Id), version.GetValueOrDefault()))
            .WhereNotNull()
            .ToArray();
        await _tracker.TrackAsync(contentUpdates);

        await _tracker.TrackAsync(new ContentAdministrativeAction(
            Language.Undefined,
            Currency.Undefined,
            new FilterCollection(new ContentIdFilter(contentUpdates.Select(x => x.Content.Id))),
            ContentAdministrativeAction.UpdateKind.EnableInRecommendations));
    }

    public async Task ExportAll(CancellationToken token)
    {
        var allContent = new List<IContent>();

        var rootContent = _contentService.GetRootContent().ToList();
        allContent.AddRange(rootContent);

        foreach (var content in rootContent)
        {
            var descendants = _contentService.GetPagedDescendants(content.Id, 0, int.MaxValue, out _).ToList();
            allContent.AddRange(descendants);
        }

        IContent[] contents = allContent.ToArray();

        if (contents.Length > 0)
        {
            long version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await Export(contents, version);

            await _tracker.TrackAsync(new ContentAdministrativeAction(
                Language.Undefined,
                Currency.Undefined,
                new FilterCollection(new ContentDataFilter(Constants.VersionKey, new EqualsCondition(version, negated: true), filterOutIfKeyIsNotFound: false)),
                ContentAdministrativeAction.UpdateKind.PermanentlyDelete), 
                token);
        }
    }
}