using System;
using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Represents the root configuration of the Relewise Umbraco Integration.
/// </summary>
public class RelewiseUmbracoOptionsBuilder
{
    internal Dictionary<string, ContentTypeBuilder?> ContentBuilders { get; } = new();

    /// <summary>
    /// Adds a named client, where you can override options for the different clients, e.g. timeout.
    /// </summary>
    /// <param name="contentType">Name of the Content Type Alias.</param>
    /// <param name="options">The options you'd like to set for this contentType.</param>
    /// <param name="throwIfExists">Defines whether the method should throw, if a contentType with same name exists.</param>
    public RelewiseUmbracoOptionsBuilder AddContentType(string contentType, Action<ContentTypeBuilder>? options = null, bool throwIfExists = false)
    {
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Value cannot be null or whitespace", nameof(contentType));

        if (ContentBuilders.ContainsKey(contentType) && throwIfExists)
            throw new ArgumentException("A contentType with that name was already registered", nameof(contentType));

        if (ContentBuilders.TryGetValue(contentType, out ContentTypeBuilder? builder))
        {
            if (throwIfExists)
                throw new ArgumentException("A contentType with that name was already registered", nameof(contentType));
        }
        else
        {
            builder = new ContentTypeBuilder();
            ContentBuilders.Add(contentType, builder);
        }

        if (builder != null)
            options?.Invoke(builder);

        return this;
    }
}

/// <summary>
/// Represents the root configuration of a ContentType
/// </summary>
public class ContentTypeBuilder
{
    internal bool AutoMapContent { get; private set; }
    internal IContentTypeMapping? Mapper { get; private set; }

    /// <summary>
    /// Set the contentType to use the builtin auto-mapping of properties
    /// </summary>
    public void AutoMap()
    {
        AutoMapContent = true;
    }

    /// <summary>
    /// Configures a mapper to be used for custom handling of a contentType
    /// </summary>
    /// <param name="contentTypeMapping"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void UseMapper(IContentTypeMapping contentTypeMapping)
    {
        Mapper = contentTypeMapping ?? throw new ArgumentNullException(nameof(contentTypeMapping));
    }
}