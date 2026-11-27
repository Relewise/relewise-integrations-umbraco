using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Site;

// HTML endpoints delegate to MVC's normal Razor executor; API payloads keep their existing JSON contract.
internal sealed record StorefrontResponse(StorefrontPage? Page = null, string? Json = null) : IResult
{
    public async Task ExecuteAsync(HttpContext http)
    {
        if (Page == null)
        {
            if (Json != null)
            {
                await Results.Content(Json, "application/json", statusCode: http.Response.StatusCode)
                    .ExecuteAsync(http);
            }

            return;
        }

        var metadata = http.RequestServices.GetRequiredService<IModelMetadataProvider>();
        var result = new ViewResult
        {
            ViewName = "/Views/Storefront/Page.cshtml",
            ViewData = new ViewDataDictionary<StorefrontPage>(metadata, new ModelStateDictionary()) { Model = Page },
            TempData = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http),
            StatusCode = http.Response.StatusCode
        };
        var action = new ActionContext(http, http.GetRouteData(), new ActionDescriptor());

        await http.RequestServices.GetRequiredService<IActionResultExecutor<ViewResult>>().ExecuteAsync(action, result);
    }
}
