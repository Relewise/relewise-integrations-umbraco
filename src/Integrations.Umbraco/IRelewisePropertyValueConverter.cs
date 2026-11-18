namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Convert a PublishedContent Property Value to one or more Relewise Content Data-fields
/// </summary>
[System.Obsolete("Implement IAsyncRelewisePropertyValueConverter and register with AddAsyncValueConverter instead. This interface will be removed in the next major version.")]
public interface IRelewisePropertyValueConverter
{
    /// <summary>
    /// Determines if the specific property can be handled by the handler.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    bool CanHandle(RelewisePropertyConverterContext context);

    /// <summary>
    /// Converts the Property to one or more Relewise Content Data-fields
    /// </summary>
    /// <param name="context"></param>
    void Convert(RelewisePropertyConverterContext context);
}
