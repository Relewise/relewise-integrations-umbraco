using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco.Services;

public class MapContentResult
{
    private MapContentResult() {}

    public MapContentResult(ContentUpdate? contentUpdate)
    {
        ContentUpdate = contentUpdate;
    }

    public ContentUpdate? ContentUpdate { get; }
    public bool Successful => ContentUpdate != null;

    public static MapContentResult Failed => new();
}