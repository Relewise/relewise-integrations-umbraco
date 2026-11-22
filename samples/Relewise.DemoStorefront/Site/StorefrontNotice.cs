namespace Relewise.DemoStorefront.Site;

/// <summary>
/// A localized, short-lived notification shown after a shopping action.
/// </summary>
public sealed record StorefrontNotice(string Text, bool Error, bool CartAdded);
