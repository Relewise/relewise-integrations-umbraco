using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;

internal static class StorefrontAdmin
{
    internal static async Task<IResult> Run(string id, string action, IRelewiseClientFactory clients)
    {
        if (!id.StartsWith("U19-SMOKE-", StringComparison.Ordinal)) return Results.BadRequest();
        var tracker = clients.GetClient<ITracker>();
        if (action == "external")
            await tracker.TrackAsync(new ProductUpdate(new Product(id)
            {
                Data = new() { ["externalField"] = "preserve-me" },
                Assortments = [7, 9],
                CategoryPaths = [new CategoryPath(new CategoryNameAndId("U19-SMOKE-EQUIPMENT", null))]
            },
                new ProductVariant("external-variant") { Data = new() { ["externalField"] = "external-variant-data" } }, ProductUpdate.UpdateKind.ReplaceProvidedProperties));
        else if (action is "enable" or "disable")
            await tracker.TrackAsync(new ProductAdministrativeAction(null, null, id, action == "enable" ? ProductAdministrativeAction.UpdateKind.Enable : ProductAdministrativeAction.UpdateKind.Disable));
        else return Results.BadRequest();
        return Results.Ok(new { id, action });
    }
}
