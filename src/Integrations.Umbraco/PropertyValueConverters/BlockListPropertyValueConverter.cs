using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models.Blocks;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class BlockListPropertyValueConverter(IServiceProvider serviceProvider) : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.BlockList");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        BlockListModel? blockList = context.Property.GetValue<BlockListModel>(context.Culture);

        if (blockList != null)
        {
            // NOTE: This needs to be resolved manually, since it would cause a circular dependency if injected through constructor
            var propertyConverter = serviceProvider.GetRequiredService<IRelewisePropertyConverter>();

            var properties = new List<DataValue?>();

            foreach (BlockListItem blockItem in blockList)
            {
                IReadOnlyDictionary<string, DataValue?> converted = propertyConverter.Convert(blockItem.Content.Properties, context.Culture);

                properties.AddRange(converted.Values);
            }

            if (properties.Count == 0)
                return;

            string[] stringCollection = properties
                .Select(x => x?.Value?.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray();

            context.Add(context.Property.Alias, new DataValue(stringCollection));
        }
    }
}