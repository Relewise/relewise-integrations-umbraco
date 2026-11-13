using Microsoft.Extensions.Options;
using Relewise.Client.Extensions;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;

internal static class LiveSearchChecks
{
    public static async Task<object> Run(RelewiseSearchProvider provider, IRelewiseClientFactory clients)
    {
        // The host's real published index may be rebuilt by Umbraco's startup job.
        // Synthetic records need an independent scope so that legitimate rebuild cannot remove them.
        string runId = Guid.NewGuid().ToString("N");
        provider = new RelewiseSearchProvider(clients, Options.Create(new RelewiseSearchOptions { Scope = "umbraco19-verification-" + runId }));
        const string index = global::Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
        Guid publicKey = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid protectedKey = Guid.Parse("20000000-0000-0000-0000-000000000002");
        Guid variantKey = Guid.Parse("20000000-0000-0000-0000-000000000003");
        Guid groupKey = Guid.Parse("20000000-0000-0000-0000-000000000004");
        var checks = new List<string>();
        IndexField[] Fields(int number, string tag) =>
        [
            new("title", new() { TextsR1 = ["Providercheck alpha"] }, null, null),
            new("number", new() { Integers = [number] }, null, null),
            new("tag", new() { Keywords = [tag] }, null, null)
        ];
        await provider.AddOrUpdateAsync(index, publicKey, UmbracoObjectTypes.Document, [new(null, null)], Fields(10, "public"), null);
        await provider.AddOrUpdateAsync(index, protectedKey, UmbracoObjectTypes.Document, [new(null, null)], Fields(20, "private"), new([groupKey]));
        await provider.AddOrUpdateAsync(index, variantKey, UmbracoObjectTypes.Document,
            [new("en", null), new("en", "vip"), new("da", null)],
            [new("title", new() { Texts = ["Providercheck English"] }, "en", null),
                new("title", new() { Texts = ["Providercheck Dansk"] }, "da", null),
                new("offer", new() { Texts = ["Exclusiveoffer"] }, "en", "vip")], null);
        // The control scope proves reset cannot delete a different site's remote entities.
        var control = new RelewiseSearchProvider(clients, Options.Create(new RelewiseSearchOptions { Scope = "umbraco19-control-" + runId }));
        await control.AddOrUpdateAsync(index, publicKey, UmbracoObjectTypes.Document, [new(null, null)], Fields(99, "control"), null);
        SearchResult ready = await provider.SearchAsync(index, "Providercheck", accessContext: AccessContext.BypassProtection());
        IndexMetadata readyMetadata = await provider.GetMetadataAsync(index);
        for (int attempt = 0; (ready.Total != 2 || readyMetadata.DocumentCount != 5) && attempt < 900; attempt++)
        {
            await Task.Delay(1000);
            ready = await provider.SearchAsync(index, "Providercheck", accessContext: AccessContext.BypassProtection());
            readyMetadata = await provider.GetMetadataAsync(index);
        }
        Check(ready.Total == 2 && readyMetadata.DocumentCount == 5, "Text and termless views have indexed all variations", checks);
        var anonymous = await provider.SearchAsync(index, "Providercheck");
        Check(anonymous.Total == 1 && anonymous.Documents.Single().Id == publicKey, "Anonymous protection", checks);
        var emptyPrincipal = await provider.SearchAsync(index, "Providercheck", accessContext: new AccessContext(Guid.Empty, [groupKey]));
        Check(emptyPrincipal.Total == 1 && emptyPrincipal.Documents.Single().Id == publicKey,
            "Empty principal cannot use group permissions", checks);
        var member = await provider.SearchAsync(index, "Providercheck", accessContext: new AccessContext(Guid.NewGuid(), [groupKey]));
        Check(member.Total == 2, "Member group access", checks);
        var english = await provider.SearchAsync(index, "English", culture: "en");
        var danish = await provider.SearchAsync(index, "Dansk", culture: "da");
        Check(english.Total == 1 && danish.Total == 1, "Culture selection", checks);
        Check((await provider.SearchAsync(index, "Exclusiveoffer", culture: "en")).Total == 0 &&
              (await provider.SearchAsync(index, "Exclusiveoffer", culture: "en", segment: "vip")).Total == 1, "Segment selection", checks);
        var range = await provider.SearchAsync(index, filters: [new IntegerRangeFilter("number", [new(10, 20)], false)], accessContext: AccessContext.BypassProtection());
        Check(range.Total == 1 && range.Documents.Single().Id == publicKey,
            $"Inclusive lower and exclusive upper range (hits: {range.Total})", checks);
        var exact = await provider.SearchAsync(index, filters: [new KeywordFilter("tag", ["private"], true)]);
        Check(exact.Documents.All(d => d.Id != protectedKey), "Negated keyword filter", checks);
        var sorted = await provider.SearchAsync(index, "Providercheck", sorters: [new IntegerSorter("number", global::Umbraco.Cms.Core.Direction.Descending)], accessContext: AccessContext.BypassProtection());
        Check(sorted.Documents.First().Id == protectedKey, "Numeric sorting", checks);
        var facets = await provider.SearchAsync(index, "Providercheck", facets: [new KeywordFacet("tag"), new IntegerRangeFacet("number", [new("low", 0, 20), new("high", 20, null)])], accessContext: AccessContext.BypassProtection());
        Check(facets.Facets.First().Values.Count() == 2, "Keyword facet buckets", checks);
        Check(facets.Facets.Last().Values.Cast<IntegerRangeFacetValue>().All(v => v.Count == 1), "Numeric range facet buckets", checks);
        var paged = await provider.SearchAsync(index, "Providercheck", skip: 1, take: 1, accessContext: AccessContext.BypassProtection());
        Check(paged.Total == 2 && paged.Documents.Count() == 1, "Pagination and total", checks);
        await provider.AddOrUpdateAsync(index, variantKey, UmbracoObjectTypes.Document, [new("en", null)],
            [new("title", new() { Texts = ["Providercheck English"] }, "en", null)], null);
        Check((await provider.SearchAsync(index, "Dansk", culture: "da")).Total == 0 &&
              (await provider.SearchAsync(index, "Exclusiveoffer", culture: "en", segment: "vip")).Total == 0,
            "Replacement removes obsolete cultures and segments", checks);
        await provider.DeleteAsync(index, [protectedKey]);
        Check((await provider.SearchAsync(index, "Providercheck", accessContext: AccessContext.BypassProtection())).Total == 1,
            "Delete removes the requested document", checks);
        await provider.ResetAsync(index);
        // Delete/reset use model filters, not the search index. Allow the remote model update to settle.
        await Task.Delay(1000);
        Check((await provider.GetMetadataAsync(index)).DocumentCount == 0, "Reset clears its own scope", checks);
        Check((await control.GetMetadataAsync(index)).DocumentCount > 0, "Reset preserves another scope", checks);
        await control.ResetAsync(index);
        return new { Passed = checks.Count, Checks = checks };
    }

    private static void Check(bool condition, string name, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException("Live search check failed: " + name);
        checks.Add(name);
    }
}
