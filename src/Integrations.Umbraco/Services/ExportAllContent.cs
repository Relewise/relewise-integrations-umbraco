namespace Relewise.Integrations.Umbraco.Services;

/// <summary>
///  Holds the request for a full content export
/// </summary>
public class ExportAllContent
{
    /// <inheritdoc cref="ExportAllContent" />
    public ExportAllContent(bool permanentlyDelete)
    {
        PermanentlyDelete = permanentlyDelete;
    }

    /// <summary>
    /// Permanently deletes content from Relewise if not part of the export
    /// </summary>
    public bool PermanentlyDelete { get; }
}