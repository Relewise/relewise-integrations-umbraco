using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Responses;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Indexing;

internal static class EntityExportDelivery
{
    internal static async Task ReconcileAsync(
        IRelewiseClientFactory clients,
        IEnumerable<EntityReconciliationRequest> requests,
        CancellationToken token)
    {
        var tracker = clients.GetClient<ITracker>(Constants.ClientNames.Integration);
        var actions = new List<Trackable>();
        foreach (EntityReconciliationRequest request in requests)
        {
            // A permanent deletion removes the entity itself; no page-availability update is needed first.
            if (!(request.Synchronize && request.Delete) &&
                (request.EntityType == typeof(Content) || request.EntityType == typeof(Product)))
            {
                await MarkUnavailableAsync(
                    clients.GetClient<IDataAccessor>(Constants.ClientNames.Integration),
                    tracker,
                    request.EntityType,
                    request.Filters,
                    token);
            }

            if (request.Synchronize)
            {
                actions.Add(Administrative(request.EntityType, request.Filters, delete: request.Delete, enable: false));
            }
        }

        if (actions.Count > 0)
        {
            await tracker.TrackAsync(actions, token);
        }
    }

    internal static async Task MarkUnavailableAsync(
        IDataAccessor reader,
        ITracker tracker,
        Type type,
        FilterCollection filters,
        CancellationToken token)
    {
        // Availability Data must be patched by entity ID; administrative actions cannot patch Data by filter.
        // Only lifecycle/rebuild reconciliation needs this lookup; ordinary publication already knows the IDs.
        var updates = new List<Trackable>();

        if (type == typeof(Product))
        {
            var query = new ProductQuery(filters, excludeProductsWithNoVariants: false, pageSize: 500)
            {
                IncludeDisabledProducts = true,
                ResultSettings = new ProductQuery.SelectedPropertiesSettings
                { SelectedProductDetailsProperties = new SelectedProductDetailsPropertiesSettings() }
            };

            do
            {
                ProductDetailsCollectionResponse response = await reader.QueryAsync(query, token);

                foreach (ProductResultDetails? product in response.Products ?? [])
                {
                    updates.Add(
                        new ProductUpdate(
                            new Product(product.ProductId)
                            {
                                Assortments = null,
                                CategoryPaths = null,
                                Data = new Dictionary<string, DataValue?> { [RelewiseDataKeys.Published] = false }
                            },
                            ProductUpdate.UpdateKind.ReplaceProvidedProperties));
                }

                query.NextPageToken = response.NextPageToken;
            } while (query.NextPageToken != null);
        }
        else
        {
            var query = new ContentQuery(filters, 500) { IncludeDisabledContents = true };

            while (true)
            {
                ContentDetailsCollectionResponse response = await reader.QueryAsync(query, token);

                foreach (ContentResultDetails? content in response.Contents ?? [])
                {
                    updates.Add(
                        new ContentUpdate(
                            new Content(content.ContentId)
                            {
                                Assortments = null,
                                CategoryPaths = null,
                                Data = new Dictionary<string, DataValue?> { [RelewiseDataKeys.Published] = false }
                            },
                            ContentUpdate.UpdateKind.ReplaceProvidedProperties));
                }

                if ((response.Contents?.Length ?? 0) < query.NumberOfResults)
                {
                    break;
                }

                query.SkipNumberOfResults += query.NumberOfResults;
            }
        }

        if (updates.Count > 0)
        {
            await tracker.TrackAsync(updates, token);
        }
    }

    internal static Filter DataFilter(
        Type type,
        string key,
        ValueCondition condition,
        bool filterOutIfKeyIsNotFound = true)
    {
        return type == typeof(Product) ? new ProductDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : type == typeof(Content) ? new ContentDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : type == typeof(Brand) ? new BrandDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : type == typeof(ProductCategory) ? new ProductCategoryDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : new ContentCategoryDataFilter(key, condition, filterOutIfKeyIsNotFound);
    }

    internal static Trackable DeleteIdentity(
        Type type,
        string id)
    {
        return type == typeof(Product)
            ? new ProductAdministrativeAction(null, null, id, ProductAdministrativeAction.UpdateKind.Delete)
            : type == typeof(Content)
                ? new ContentAdministrativeAction(
                    null,
                    null,
                    new FilterCollection(new ContentIdFilter(id)),
                    ContentAdministrativeAction.UpdateKind.Delete)
                : type == typeof(Brand)
                    ? new BrandAdministrativeAction(
                        null,
                        null,
                        new FilterCollection(new BrandIdFilter(id)),
                        BrandAdministrativeAction.UpdateKind.Delete)
                    : type == typeof(ProductCategory)
                        ? new ProductCategoryAdministrativeAction(
                            null,
                            null,
                            id,
                            CategoryAdministrativeAction.UpdateKind.Delete)
                        : new ContentCategoryAdministrativeAction(
                            null,
                            null,
                            id,
                            CategoryAdministrativeAction.UpdateKind.Delete);
    }

    internal static Trackable Administrative(
        Type type,
        FilterCollection filters,
        bool delete,
        bool enable)
    {
        return type == typeof(Product) ? new ProductAdministrativeAction(
                null,
                null,
                filters,
                enable ? ProductAdministrativeAction.UpdateKind.Enable :
                delete ? ProductAdministrativeAction.UpdateKind.Delete : ProductAdministrativeAction.UpdateKind.Disable)
            : type == typeof(Content) ? new ContentAdministrativeAction(
                null,
                null,
                filters,
                enable ? ContentAdministrativeAction.UpdateKind.Enable :
                delete ? ContentAdministrativeAction.UpdateKind.Delete : ContentAdministrativeAction.UpdateKind.Disable)
            : type == typeof(Brand) ? new BrandAdministrativeAction(
                null,
                null,
                filters,
                enable ? BrandAdministrativeAction.UpdateKind.Enable :
                delete ? BrandAdministrativeAction.UpdateKind.Delete : BrandAdministrativeAction.UpdateKind.Disable)
            : type == typeof(ProductCategory) ? new ProductCategoryAdministrativeAction(
                null,
                null,
                filters,
                enable ? CategoryAdministrativeAction.UpdateKind.Enable :
                delete ? CategoryAdministrativeAction.UpdateKind.Delete :
                CategoryAdministrativeAction.UpdateKind.Disable)
            : new ContentCategoryAdministrativeAction(
                null,
                null,
                filters,
                enable ? CategoryAdministrativeAction.UpdateKind.Enable :
                delete ? CategoryAdministrativeAction.UpdateKind.Delete :
                CategoryAdministrativeAction.UpdateKind.Disable);
    }
}
