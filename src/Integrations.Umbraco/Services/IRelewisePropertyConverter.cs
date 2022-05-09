using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

internal interface IRelewisePropertyConverter
{
    Dictionary<string, DataValue> Convert(IEnumerable<IPublishedProperty> properties, string[] cultures);
    public Dictionary<string, DataValue> Convert(IEnumerable<IPublishedProperty> properties, string culture) => Convert(properties, new[] { culture });

}