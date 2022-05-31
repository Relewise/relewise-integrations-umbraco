﻿using System;
using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco;

public class RelewiseUmbracoOptionsBuilder
{
    internal Dictionary<string, ContentTypeBuilder?> ContentBuilders { get; } = new();

    public RelewiseUmbracoOptionsBuilder Add(string contentType, Action<ContentTypeBuilder>? options = null, bool throwIfExists = false)
    {
        if (ContentBuilders.ContainsKey(contentType) && throwIfExists)
            throw new ArgumentException("A client with that name was already registered", nameof(contentType));

        if (ContentBuilders.TryGetValue(contentType, out ContentTypeBuilder? builder))
        {
            if (throwIfExists)
                throw new ArgumentException("A client with that name was already registered", nameof(contentType));
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

public class ContentTypeBuilder
{
    internal bool AutoMapContent { get; private set; }
    internal IContentTypeMapping? Mapper { get; private set; }

    public void AutoMap()
    {
        AutoMapContent = true;
    }

    public void UseMapper(IContentTypeMapping contentTypeMapping)
    {
        Mapper = contentTypeMapping;
    }
}