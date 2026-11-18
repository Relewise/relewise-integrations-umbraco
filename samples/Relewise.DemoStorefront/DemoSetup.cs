using System.Security.Cryptography;
using System.Text;

internal static class DemoSetup
{
    internal static bool HasRelewiseConfiguration(IConfiguration configuration)
    {
        string? id = configuration["Relewise:DatasetId"];
        string? key = configuration["Relewise:ApiKey"];
        string? server = configuration["Relewise:ServerUrl"];
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(server))
        {
            return false;
        }

        if (!Guid.TryParse(id, out _) || string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(server, UriKind.Absolute, out var endpoint) || !endpoint.IsLoopback)
        {
            throw new InvalidOperationException("Configure DatasetId, ApiKey and a local ServerUrl together in local.settings.json. The demo only supports local Datasets.");
        }

        return true;
    }

    internal static void ConfigureLocalInstallation(WebApplicationBuilder builder)
    {
        // The location separates independently cloned demo databases without adding integration-owned records.
        string root = builder.Environment.ContentRootPath;
        string id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root)))[..16]).ToString();
        string data = Path.Combine(root, "umbraco", "Data");
        Directory.CreateDirectory(data);
        string imagingKeyFile = Path.Combine(data, "demo-imaging.key");
        if (!File.Exists(imagingKeyFile))
        {
            File.WriteAllText(imagingKeyFile, Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
        }

        var defaults = new Dictionary<string, string?>
        {
            ["Umbraco:CMS:Global:Id"] = id,
            ["Umbraco:CMS:Imaging:HMACSecretKey"] = File.ReadAllText(imagingKeyFile),
            ["Umbraco:CMS:Unattended:InstallUnattended"] = "true",
            ["Umbraco:CMS:Unattended:UpgradeUnattended"] = "true",
            ["Umbraco:CMS:Unattended:UnattendedUserName"] = "Demo editor",
            ["Umbraco:CMS:Unattended:UnattendedUserEmail"] = "demo@example.invalid",
            ["Umbraco:CMS:Unattended:UnattendedUserPassword"] = "LocalDemo!" + id + "Aa1",
            ["ConnectionStrings:umbracoDbDSN"] = "Data Source=" + Path.Combine(data, "Umbraco.sqlite.db") + ";Cache=Shared;Foreign Keys=True;Pooling=True",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite"
        };
        builder.Configuration.AddInMemoryCollection(defaults.Where(value => builder.Configuration[value.Key] == null));
    }
}
