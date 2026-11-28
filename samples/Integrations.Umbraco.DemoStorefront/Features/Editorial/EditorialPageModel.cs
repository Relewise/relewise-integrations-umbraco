using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Editorial;

/// <summary>
/// Published editorial content and its visible block sections.
/// </summary>
public sealed record EditorialPageModel(IPublishedContent Page, EditorialSection[] Sections);
