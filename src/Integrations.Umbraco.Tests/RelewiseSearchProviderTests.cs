using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Filters.DataObjects;
using Relewise.Client.Requests.Filters.DataObjects.Conditions;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using RFilter = Relewise.Client.Requests.Filters.Filter;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;
using Searcher = Relewise.Client.Search.ISearcher;

namespace Relewise.Integrations.Umbraco.Tests;

public class RelewiseSearchProviderTests
{
    private const string Index = global::Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
    private readonly Mock<IRelewiseClientFactory> _factory = new();
    private readonly Mock<ITracker> _tracker = new();
    private readonly Mock<Searcher> _searcher = new();

    private RelewiseSearchProvider Provider(string scope = "test-site")
    {
        _factory.Setup(f => f.GetClient<ITracker>(It.IsAny<string>())).Returns(_tracker.Object);
        _factory.Setup(f => f.GetClient<Searcher>(It.IsAny<string>())).Returns(_searcher.Object);
        return new RelewiseSearchProvider(_factory.Object, Options.Create(new RelewiseSearchOptions { Scope = scope }));
    }

    [Fact]
    public void CultureAndSegmentFieldsAreIsolatedAndInvariantFieldsAreRetained()
    {
        Guid id = Guid.NewGuid();
        ContentUpdate[] updates = Provider().MapDocuments(Index, id, UmbracoObjectTypes.Document,
            [new("en", null), new("en", "vip"), new("da", null)],
            [new("title", new() { TextsR1 = ["Shared"] }, null, null),
                new("body", new() { Texts = ["English"] }, "en", null),
                new("body", new() { Texts = ["Dansk"] }, "da", null),
                new("offer", new() { Texts = ["VIP"] }, "en", "vip")], null, "generation").ToArray();

        Assert.Equal(3, updates.Length);
        Assert.Equal(3, updates.Select(u => u.Content.Id).Distinct().Count());
        var data = updates.Select(u => u.Content.Data!).ToArray();
        Assert.Equal("Shared", data[0][RelewiseSearchProvider.Field("title", "text")]!.ValueAs<string>());
        Assert.False(data[0].ContainsKey(RelewiseSearchProvider.Field("offer", "text")));
        Assert.Equal("VIP", data[1][RelewiseSearchProvider.Field("offer", "text")]!.ValueAs<string>());
        Assert.Equal("Dansk", data[2][RelewiseSearchProvider.Field("body", "text")]!.ValueAs<string>());
        Assert.All(data, d => Assert.Equal(id.ToString("D"), d[RelewiseSearchProvider.IdKey]!.ValueAs<string>()));
    }

    [Fact]
    public void AnonymousAndMemberQueriesApplyProtectionAndKeepScope()
    {
        Guid member = Guid.NewGuid();
        Guid group = Guid.NewGuid();
        RelewiseSearchProvider provider = Provider();
        ContentSearchRequest anonymous = provider.QueryRequest(Index, "term", [], "da", "vip", null, 3, 5);
        Assert.Equal(3, anonymous.Skip);
        Assert.Equal(5, anonymous.Take);
        var data = BaseData();
        data[RelewiseSearchProvider.CultureKey] = "da";
        Assert.True(Matches(anonymous.Filters!, data)); // Default segment fallback.
        data[RelewiseSearchProvider.ProtectedKey] = true;
        data[RelewiseSearchProvider.AccessKey] = new DataValue(new[] { group.ToString("D") });
        Assert.False(Matches(anonymous.Filters!, data));
        var authenticated = provider.QueryRequest(Index, null, [], "da", "vip", new AccessContext(member, [group]), 0, 10);
        Assert.True(Matches(authenticated.Filters!, data));
        data[RelewiseSearchProvider.OwnerKey] = "other-site";
        Assert.False(Matches(authenticated.Filters!, data));
        var bypass = provider.QueryRequest(Index, null, [], "da", null, AccessContext.BypassProtection(), 0, 10);
        Assert.False(Matches(bypass.Filters!, data)); // Bypass never bypasses scope.
    }

    [Fact]
    public void EmptyPrincipalCannotUseGroupPermissionsButExplicitBypassStillWorks()
    {
        Guid group = Guid.NewGuid();
        var provider = Provider();
        var data = BaseData();
        data[RelewiseSearchProvider.ProtectedKey] = true;
        data[RelewiseSearchProvider.AccessKey] = new DataValue(new[] { group.ToString("D") });

        Assert.False(Matches(provider.QueryRequest(Index, null, [], null, null, new AccessContext(Guid.Empty, [group]), 0, 10).Filters!, data));
        Assert.False(Matches(provider.QueryRequest(Index, null, [], null, null, new AccessContext(Guid.NewGuid(), []), 0, 10).Filters!, data));
        Assert.True(Matches(provider.QueryRequest(Index, null, [], null, null, new AccessContext(Guid.NewGuid(), [group]), 0, 10).Filters!, data));
        Assert.True(Matches(provider.QueryRequest(Index, null, [], null, null, AccessContext.BypassProtection(), 0, 10).Filters!, data));
        data[RelewiseSearchProvider.ProtectedKey] = false;
        Assert.True(Matches(provider.QueryRequest(Index, null, [], null, null, new AccessContext(Guid.Empty, [group]), 0, 10).Filters!, data));
    }

    [Fact]
    public void SegmentSelectionAvoidsReturningBothDefaultAndOverride()
    {
        var provider = Provider();
        var query = provider.QueryRequest(Index, null, [], null, "vip", null, 0, 10);
        var data = BaseData();
        data[RelewiseSearchProvider.SegmentsKey] = new DataValue(new[] { "", "vip" });
        Assert.False(Matches(query.Filters!, data));
        data[RelewiseSearchProvider.SegmentKey] = "vip";
        Assert.True(Matches(query.Filters!, data));
        data[RelewiseSearchProvider.CultureKey] = "da";
        Assert.False(Matches(query.Filters!, data));
    }

    [Fact]
    public void NumericRangesRequireOneValueWithinInclusiveLowerExclusiveUpperBounds()
    {
        var filter = RelewiseSearchProvider.MapFilter(new IntegerRangeFilter("price", [new(10, 20)], false));
        Dictionary<string, DataValue?> Data(params double[] values) => new()
        {
            [RelewiseSearchProvider.Field("price", "integerValues")] = new DataValue(values.Select(v =>
                new DataObject(new Dictionary<string, DataValue?> { ["value"] = v })))
        };
        Assert.True(Matches(filter, Data(10)));
        Assert.True(Matches(filter, Data(19)));
        Assert.False(Matches(filter, Data(20)));
        Assert.False(Matches(filter, Data(5, 25)));
        Assert.False(Matches(filter, Data()));
        Assert.True(Matches(RelewiseSearchProvider.MapFilter(new IntegerRangeFilter("price", [new(10, 20)], true)), Data(5, 25)));
    }

    [Fact]
    public async Task ResetAndDeleteAreAlwaysScopedAndEmptyDeleteDoesNothing()
    {
        var actions = new List<ContentAdministrativeAction>();
        _tracker.Setup(t => t.TrackAsync(It.IsAny<ContentAdministrativeAction>(), It.IsAny<CancellationToken>()))
            .Callback<ContentAdministrativeAction, CancellationToken>((a, _) => actions.Add(a)).Returns(Task.CompletedTask);
        var provider = Provider();
        await provider.DeleteAsync(Index, []);
        Assert.Empty(actions);
        await provider.ResetAsync(Index);
        await provider.DeleteAsync(Index, [Guid.Parse("10000000-0000-0000-0000-000000000001")]);
        foreach (var action in actions)
        {
            Assert.Equal(ContentAdministrativeAction.UpdateKind.Delete, action.Kind);
            var data = BaseData();
            data[RelewiseSearchProvider.IdKey] = "10000000-0000-0000-0000-000000000001";
            Assert.True(Matches(action.Filters!, data));
            data[RelewiseSearchProvider.IndexKey] = "other-index";
            Assert.False(Matches(action.Filters!, data));
        }
    }

    [Fact]
    public async Task FailedIndexWriteDoesNotDeletePreviouslyIndexedVariations()
    {
        _tracker.Setup(t => t.TrackAsync(It.IsAny<IEnumerable<Trackable>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unavailable"));
        var provider = Provider();
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.AddOrUpdateAsync(Index, Guid.NewGuid(), UmbracoObjectTypes.Document,
            [new(null, null)], [], null));
        _tracker.Verify(t => t.TrackAsync(It.IsAny<ContentAdministrativeAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReplacementAndCleanupShareOneOrderedTrackingBatch()
    {
        Trackable[]? batch = null;
        _tracker.Setup(t => t.TrackAsync(It.IsAny<IEnumerable<Trackable>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Trackable>, CancellationToken>((items, _) => batch = items.ToArray()).Returns(Task.CompletedTask);
        Guid id = Guid.NewGuid();
        await Provider().AddOrUpdateAsync(Index, id, UmbracoObjectTypes.Document, [new(null, null)], [], null);
        Assert.NotNull(batch);
        var update = Assert.IsType<ContentUpdate>(batch[0]);
        Assert.Equal(ContentAdministrativeAction.UpdateKind.Enable, Assert.IsType<ContentAdministrativeAction>(batch[1]).Kind);
        var cleanup = Assert.IsType<ContentAdministrativeAction>(batch[2]);
        Assert.Equal(ContentAdministrativeAction.UpdateKind.Delete, cleanup.Kind);
        Assert.False(Matches(cleanup.Filters!, update.Content.Data!));
        update.Content.Data![RelewiseSearchProvider.GenerationKey] = "old-generation";
        Assert.True(Matches(cleanup.Filters!, update.Content.Data!));
    }

    [Fact]
    public async Task SearchMapsGuidIdentityAndFacetsAndPassesPagingToRelewise()
    {
        Guid id = Guid.NewGuid();
        var requests = new List<ContentSearchRequest>();
        _searcher.Setup(s => s.SearchAsync(It.IsAny<ContentSearchRequest>(), It.IsAny<CancellationToken>()))
            .Returns<ContentSearchRequest, CancellationToken>((request, _) =>
            {
                requests.Add(request);
                var response = new ContentSearchResponse
                {
                    Hits = 12,
                    Results = [new ContentResult("scoped-provider-id", 0) { Data = new()
                    {
                        [RelewiseSearchProvider.IdKey] = id.ToString("D"),
                        [RelewiseSearchProvider.TypeKey] = "Document"
                    } }],
                    Facets = new()
                };
                if (request.Facets!.Items!.Count > 0)
                    response.Facets.Items = [new Relewise.Client.DataTypes.Search.Facets.Result.ContentDataStringValueFacetResult(
                        RelewiseSearchProvider.Field("tag", "keyword"), [], null, [new("news", false, 12)])];
                return Task.FromResult(response);
            });
        var result = await Provider().SearchAsync(Index, "hello", [new KeywordFilter("tag", ["news"], false)],
            [new KeywordFacet("tag")], culture: "en", skip: 4, take: 6);
        Assert.Equal(12, result.Total);
        Assert.Equal(id, Assert.Single(result.Documents).Id);
        Assert.Equal("news", Assert.IsType<KeywordFacetValue>(Assert.Single(Assert.Single(result.Facets).Values)).Key);
        Assert.Equal(4, requests[0].Skip);
        Assert.Equal(6, requests[0].Take);
        Assert.Equal(requests[0].Filters!.Items!.Count - 1, requests[1].Filters!.Items!.Count);
    }

    [Fact]
    public async Task InvalidIndexAndUnsupportedFilterFailExplicitly()
    {
        var provider = Provider();
        await Assert.ThrowsAsync<ArgumentException>(() => provider.ResetAsync("not-registered"));
        Assert.Throws<NotSupportedException>(() => RelewiseSearchProvider.MapFilter(new UnknownFilter()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.SearchAsync(Index, skip: -1));
    }

    private sealed record UnknownFilter() : UFilter("unknown", false);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankQueriesUseTermlessSearch(string? term)
    {
        Assert.Null(Provider().QueryRequest(Index, term, [], null, null, null, 0, 10).Term);
    }

    private static Dictionary<string, DataValue?> BaseData() => new()
    {
        [RelewiseSearchProvider.OwnerKey] = "test-site", [RelewiseSearchProvider.IndexKey] = Index,
        [RelewiseSearchProvider.IdKey] = Guid.NewGuid().ToString("D"),
        [RelewiseSearchProvider.CultureKey] = "", [RelewiseSearchProvider.SegmentKey] = "",
        [RelewiseSearchProvider.SegmentsKey] = new DataValue(new[] { "" }), [RelewiseSearchProvider.ProtectedKey] = false
    };

    // Evaluates the documented filter contract for focused translation tests. A live Dataset
    // check is still required to establish backend ranking and search configuration behavior.
    private static bool Matches(FilterCollection filters, Dictionary<string, DataValue?> data) => filters.Items!.All(f => Matches(f, data));
    private static bool Matches(RFilter filter, Dictionary<string, DataValue?> data)
    {
        bool result = filter switch
        {
            AndFilter f => f.Filters!.All(x => Matches(x, data)),
            OrFilter f => f.Filters!.Any(x => Matches(x, data)),
            ContentDataFilter f when !data.TryGetValue(f.Key!, out var value) || value is null => !f.FilterOutIfKeyIsNotFound,
            ContentDataFilter f => f.MustMatchAllConditions
                ? f.Conditions!.Items!.All(c => Matches(c, data[f.Key!]!))
                : f.Conditions!.Items!.Any(c => Matches(c, data[f.Key!]!)),
            _ => throw new NotSupportedException(filter.GetType().Name)
        };
        return filter.Negated ? !result : result;
    }

    private static bool Matches(ValueCondition condition, DataValue data)
    {
        bool result = condition switch
        {
            EqualsCondition c => JToken.DeepEquals(JToken.FromObject(c.Value!.Value!), JToken.FromObject(data.Value!)),
            ContainsCondition { ObjectFilter: { } objectFilter } => data.ValueAsListOf<DataObject>()!.Any(v => objectFilter.Conditions!.All(c => Matches(c, v))),
            ContainsCondition c => data.IsCollection ? data.ValueAsListOf<string>()!.Contains(c.Value!.ValueAs<string>())
                : data.ValueAs<string>()!.Contains(c.Value!.ValueAs<string>()!, StringComparison.Ordinal),
            _ => throw new NotSupportedException(condition.GetType().Name)
        };
        return condition.Negated ? !result : result;
    }

    private static bool Matches(ObjectValueCondition condition, DataObject data)
    {
        double value = data.Data![condition.Key!]!.ValueAs<double>();
        bool result = condition switch
        {
            ObjectValueLessThanCondition c => value < c.Value,
            ObjectValueGreaterThanCondition c => value > c.Value,
            ObjectValueInRangeCondition c => value >= c.Range!.LowerBoundInclusive && value <= c.Range.UpperBoundInclusive,
            _ => throw new NotSupportedException(condition.GetType().Name)
        };
        return condition.Negated ? !result : result;
    }
}
