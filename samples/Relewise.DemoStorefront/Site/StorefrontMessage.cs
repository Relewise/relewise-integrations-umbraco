namespace Relewise.DemoStorefront.Site;

/// <summary>
/// Presentation data for an unavailable feature or failed request.
/// </summary>
public sealed record StorefrontMessage(string Message, bool Error = false, bool Help = false, bool Empty = false);
