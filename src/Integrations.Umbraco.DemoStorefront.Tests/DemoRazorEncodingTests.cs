using System.Globalization;
using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Browsing;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Tests;

public class DemoRazorEncodingTests
{
    [Fact]
    public async Task CardEncodesTextAndAttributesInsteadOfInterpretingCmsValuesAsMarkup()
    {
        const string text = "\"><img src=x onerror='alert(1)'><script>alert(2)</script>&";
        const string url = "/product?value=" + text;
        var card = new ListingCard(
            Guid.NewGuid(),
            text,
            url,
            text,
            text,
            Storefront.Icon(),
            true,
            text,
            text,
            text,
            text,
            text,
            text);

        HtmlDocument document = Parse(await RenderAsync("/Views/Browsing/_Card.cshtml", card));

        Assert.Null(document.DocumentNode.SelectSingleNode("//script|//img"));

        var title = Assert.IsType<HtmlNode>(document.DocumentNode.SelectSingleNode("//h3"));
        var link = Assert.IsType<HtmlNode>(document.DocumentNode.SelectSingleNode("//a"));
        var article = Assert.IsType<HtmlNode>(document.DocumentNode.SelectSingleNode("//article"));

        Assert.Equal(text, WebUtility.HtmlDecode(title.InnerText));
        Assert.Equal(url, WebUtility.HtmlDecode(link.GetAttributeValue("href", "")));
        Assert.Equal(text, WebUtility.HtmlDecode(article.GetAttributeValue("data-entity", "")));
    }

    [Fact]
    public async Task RecommendationJsonCannotCloseItsScriptElementAndRoundTripsUnchanged()
    {
        const string text = "</script><script>alert('unsafe')</script>&\"";
        var model = new RecommendationModel
        {
            Endpoint = "/recommendations?value=" + text,
            Labels = new Dictionary<string, string> { ["unsafe"] = text }
        };

        HtmlDocument document = Parse(await RenderAsync("/Views/Recommendations/_Placement.cshtml", model));
        HtmlNodeCollection? scripts = document.DocumentNode.SelectNodes("//script");
        Assert.NotNull(scripts);
        HtmlNode script = Assert.Single(scripts);

        Assert.Equal("application/json", script.GetAttributeValue("type", ""));

        using JsonDocument json = JsonDocument.Parse(script.InnerHtml);

        Assert.Equal(model.Endpoint, json.RootElement.GetProperty("endpoint").GetString());
        Assert.Equal(text, json.RootElement.GetProperty("labels").GetProperty("unsafe").GetString());
    }

    private static HtmlDocument Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        return document;
    }

    private static async Task<string> RenderAsync(string path, object model)
    {
        // Use compiled sample views with MVC's real encoder, without starting Umbraco or a server.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ApplicationName = typeof(StorefrontPage).Assembly.GetName().Name,
                ContentRootPath = Path.GetTempPath(),
                Args = []
            });
        builder.Services.AddControllersWithViews();

        await using WebApplication app = builder.Build();
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var engine = scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>();
        ViewEngineResult result = engine.GetView(null, path, false);

        if (!result.Success)
        {
            throw new InvalidOperationException("Compiled sample view was not found: " + path);
        }

        var metadata = scope.ServiceProvider.GetRequiredService<IModelMetadataProvider>();
        var data = new ViewDataDictionary(metadata, new ModelStateDictionary()) { Model = model };
        ITempDataDictionary tempData = scope.ServiceProvider.GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(http);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var view = new ViewContext(action, result.View, data, tempData, writer, new HtmlHelperOptions());

        await result.View.RenderAsync(view);

        return writer.ToString();
    }
}
