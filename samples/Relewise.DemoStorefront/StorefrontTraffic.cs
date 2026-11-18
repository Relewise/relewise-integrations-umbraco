using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using Relewise.Client;
using Relewise.Client.Extensions;

internal sealed class StorefrontTraffic : DelegatingHandler
{
    internal ConcurrentQueue<object> Requests { get; } = new();
    internal ConcurrentQueue<object> Entries { get; } = new();
    public StorefrontTraffic() : base(new HttpClientHandler()) { }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.IsLoopback != true)
        {
            throw new InvalidOperationException("Smoke traffic must stay on a local Dataset host.");
        }

        string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        var response = await base.SendAsync(request, cancellationToken);
        Requests.Enqueue(new
        {
            operation = request.RequestUri!.AbsolutePath,
            status = (int)response.StatusCode,
            elapsedMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            requestBytes = System.Text.Encoding.UTF8.GetByteCount(body)
        });
        if (!string.IsNullOrWhiteSpace(body))
        {
            JToken root = JToken.Parse(body);
            foreach (JObject value in ((JContainer)root).DescendantsAndSelf().OfType<JObject>())
            {
                if (value["User"] == null && value["Product"] == null && value["Content"] == null)
                {
                    continue;
                }

                Entries.Enqueue(new
                {
                    time = DateTimeOffset.UtcNow,
                    server = request.RequestUri!.GetLeftPart(UriPartial.Authority),
                    operation = request.RequestUri.AbsolutePath,
                    status = (int)response.StatusCode,
                    type = value["$type"]?.Value<string>(),
                    user = value["User"] == null ? null : new
                    {
                        AuthenticatedId = value["User"]?["AuthenticatedId"]?.Value<string>(),
                        TemporaryId = value["User"]?["TemporaryId"]?.Value<string>()
                    },
                    productId = value["Product"]?["Id"]?.Value<string>(),
                    contentId = value["Content"]?["Id"]?.Value<string>(),
                    variantId = value["Variant"]?["Id"]?.Value<string>(),
                    brandId = value["Brand"]?["Id"]?.Value<string>(),
                    categoryPath = value["IdPath"]?.Values<string>().ToArray(),
                    location = value["DisplayedAtLocation"]?.Value<string>(),
                    term = value["Term"]?.Value<string>(),
                    filterKeys = value["Filters"] is JContainer filters ? filters.DescendantsAndSelf().OfType<JObject>()
                        .Where(filter => filter["Key"] != null).Select(filter => filter["Key"]!.Value<string>()).Distinct().ToArray() : [],
                    variantResultsPerProduct = value["Settings"]?["VariantRequestSettings"]?["MaxVariantsPerProduct"]?.Value<int>()

                });
            }
        }
        return response;
    }
    internal static void Configure(WebApplication app)
    {
        var traffic = app.Services.GetRequiredService<StorefrontTraffic>();
        var http = app.Services.GetRequiredService<HttpClient>();
        var clients = app.Services.GetRequiredService<IRelewiseClientFactory>();
        foreach (string name in clients.ClientNames)
        {
            ConfigureClient(clients.GetClient<ITracker>(name));
            ConfigureClient(clients.GetClient<Relewise.Client.Search.ISearcher>(name));
            ConfigureClient(clients.GetClient<IDataAccessor>(name));
        }
        ConfigureClient(app.Services.GetRequiredService<ITracker>());
        void ConfigureClient(IClient client)
        {
            var sdk = (ClientBase)client;
            sdk.UseFastBinarySerializer = false;
            sdk.UseJsonCompression = false;
            sdk.OverriddenHttpClient = http;
        }
        app.MapGet("/smoke/requests", () => Results.Json(traffic.Requests));
        app.MapGet("/smoke/traffic", () => Results.Json(traffic.Entries));
        app.MapGet("/smoke/evidence", () =>
        {
            string rows = string.Concat(traffic.Entries.TakeLast(8).Select(JObject.FromObject).Select(entry =>
                "<tr>" + string.Concat(new[] {
                    entry["operation"]?.Value<string>()?.Split('/').Last(), entry["status"]?.ToString(),
                    entry["user"]?["TemporaryId"]?.Value<string>() ?? entry["user"]?["AuthenticatedId"]?.Value<string>() ?? "Anonymous",
                    string.Join(" / ", new[] { entry["productId"]?.Value<string>(), entry["variantId"]?.Value<string>(), entry["contentId"]?.Value<string>(), entry["brandId"]?.Value<string>(), entry["categoryPath"] == null || entry["categoryPath"]!.Type == JTokenType.Null ? null : string.Join(" → ", entry["categoryPath"]!.Values<string>()) }.OfType<string>()),
                    entry["location"]?.Value<string>()
                }.Select(value => "<td style='padding:14px 9px;border-bottom:1px solid #dfe4da;overflow-wrap:anywhere'>" + Storefront.E(value) + "</td>")) + "</tr>"));
            return Results.Content(Storefront.Layout("<section class='hero'><p class='eyebrow'>REAL LOCAL API RESPONSES</p><h1>Identity correlation</h1><p>Search and automatic views share the visitor identity. Product and Variant IDs match their exported entities.</p></section><table style='width:100%;table-layout:fixed;font-size:11px;text-align:left'><thead><tr><th>SDK request</th><th style='width:55px'>HTTP</th><th>Visitor ID</th><th>Entity / Variant ID</th><th>Displayed at location</th></tr></thead><tbody>" + rows + "</tbody></table>"), "text/html");
        });
    }
}
