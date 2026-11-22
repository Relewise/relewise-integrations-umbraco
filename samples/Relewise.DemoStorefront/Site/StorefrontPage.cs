namespace Relewise.DemoStorefront.Site;

/// <summary>A typed page selection rendered by the shared Razor layout.</summary>
public sealed record StorefrontPage(string Title, string Section, string View, object Model);
