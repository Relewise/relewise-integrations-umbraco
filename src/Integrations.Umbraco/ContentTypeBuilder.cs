#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.

using System;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Configures mapping and tracking for a Document Type exported as Content.
/// </summary>
public class ContentTypeBuilder : EntityMappingBuilder<Content>
{
    internal bool AutoMapContent { get; private set; }
    internal IContentTypeMapping? Mapper { get; private set; }

    /// <summary>
    /// Set the contentType to use the builtin auto-mapping of properties
    /// </summary>
    public void AutoMap()
    {
        AutoMapContent = true;
        Definition.AutomaticData = true;
    }

    /// <summary>
    /// Configures a mapper to be used for custom handling of a contentType
    /// </summary>
    /// <param name="contentTypeMapping"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [Obsolete(
        "Use UseMapper<TMapper>() with IEntityMapper<Content> instead. This overload will be " +
        "removed in the next major version.")]
    public void UseMapper(IContentTypeMapping contentTypeMapping)
    {
        Mapper = contentTypeMapping ?? throw new ArgumentNullException(nameof(contentTypeMapping));
        Definition.LegacyMapper = Mapper;
    }
}
