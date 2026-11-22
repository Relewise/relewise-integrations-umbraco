using Umbraco.Cms.Core.Strings;

namespace Relewise.DemoStorefront.Features.Editorial;

/// <summary>
/// A visible editorial heading and Umbraco rich-text value.
/// </summary>
public sealed record EditorialSection(string? Heading, IHtmlEncodedString? Text);
