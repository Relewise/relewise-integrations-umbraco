using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Language = Relewise.Client.DataTypes.Language;

namespace Relewise.Integrations.Umbraco.Services;

internal class ExportContentService : IExportContentService
{
    private readonly IContentMapper _contentMapper;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly ITracker _tracker;
    private readonly IContentService _contentService;

    public ExportContentService(IContentMapper contentMapper, IUmbracoContextFactory umbracoContextFactory, ITracker tracker, IContentService contentService)
    {
        _contentMapper = contentMapper;
        _umbracoContextFactory = umbracoContextFactory;
        _tracker = tracker; // NOTE: Erstat ved at depende på IServicesProvider -> hvis vi ikke kan resolve, så gør intet
        _contentService = contentService;
    }

    public async Task<ExportContentResult> Export(ExportContent exportContent, CancellationToken token)
    {
        if (exportContent == null) throw new ArgumentNullException(nameof(exportContent));
        if (exportContent.Contents.Length == 0)
            return new ExportContentResult();

        using UmbracoContextReference umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();

        IPublishedContentCache contentCache = umbracoContextReference.UmbracoContext.Content;

        ContentUpdate[] contentUpdates = exportContent.Contents
            .Select(x => _contentMapper.Map(new MapContent(contentCache.GetById(x.Id), exportContent.Version)))
            .Where(x => x.Successful)
            .Select(x => x.ContentUpdate!)
            .ToArray();
        await _tracker.TrackAsync(token, contentUpdates);

        await _tracker.TrackAsync(new ContentAdministrativeAction(
            Language.Undefined,
            Currency.Undefined,
            new FilterCollection(new ContentIdFilter(contentUpdates.Select(x => x.Content.Id))),
            ContentAdministrativeAction.UpdateKind.EnableInRecommendations), token);

        return new ExportContentResult();
    }

    public async Task<ExportAllContentResult> ExportAll(ExportAllContent exportAllContent, CancellationToken token)
    {
        var allContent = new List<IContent>();

        List<IContent> rootContent = _contentService.GetRootContent().ToList();
        allContent.AddRange(rootContent);

        foreach (IContent content in rootContent)
        {
            List<IContent> descendants = _contentService.GetPagedDescendants(content.Id, 0, int.MaxValue, out _).ToList();
            allContent.AddRange(descendants);
        }

        IContent[] contents = allContent.ToArray();

        if (contents.Length > 0)
        {
            long version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await Export(new ExportContent(contents, version), token);

            await _tracker.TrackAsync(new ContentAdministrativeAction(
                Language.Undefined,
                Currency.Undefined,
                new FilterCollection(new ContentDataFilter(Constants.VersionKey, new EqualsCondition(version, negated: true), filterOutIfKeyIsNotFound: false)),
                ContentAdministrativeAction.UpdateKind.PermanentlyDelete), 
                token);
        }

        return new ExportAllContentResult();
    }
}