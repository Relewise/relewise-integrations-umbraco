using Newtonsoft.Json.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Filters.DataObjects.Conditions;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using RFilter = Relewise.Client.Requests.Filters.Filter;

namespace Relewise.Integrations.Umbraco.Tests;

public class CoreSearchTranslationTests
{
    [Fact]
    public void NumericRangesRequireOneValueWithinInclusiveLowerExclusiveUpperBounds()
    {
        RFilter filter =
            CoreSearchTranslation.MapFilter(new IntegerRangeFilter("price",
                [new IntegerRangeFilterRange(10, 20)],
                false));

        Dictionary<string, DataValue?> Data(params double[] values)
        {
            return new Dictionary<string, DataValue?>
            {
                [CoreSearchTranslation.Field("price", "integerValues")] = new(values.Select(v =>
                    new DataObject(new Dictionary<string, DataValue?> { ["value"] = v })))
            };
        }

        Assert.True(Matches(filter, Data(10)));
        Assert.True(Matches(filter, Data(19)));
        Assert.False(Matches(filter, Data(20)));
        Assert.False(Matches(filter, Data(5, 25)));
        Assert.False(Matches(filter, Data()));
        Assert.True(Matches(
            CoreSearchTranslation.MapFilter(
                new IntegerRangeFilter("price", [new IntegerRangeFilterRange(10, 20)], true)),
            Data(5, 25)));
    }

    // Evaluates the documented filter contract for focused translation tests. A live Dataset
    // check is still required to establish backend ranking and search configuration behavior.
    private static bool Matches(RFilter filter,
        Dictionary<string, DataValue?> data)
    {
        var result = filter switch
        {
            AndFilter f => f.Filters.All(x => Matches(x, data)),
            OrFilter f => f.Filters.Any(x => Matches(x, data)),
            ContentDataFilter f when !data.TryGetValue(f.Key, out DataValue? value) || value is null => !f
                .FilterOutIfKeyIsNotFound,
            ContentDataFilter f => f.MustMatchAllConditions
                ? f.Conditions!.Items!.All(c => Matches(c, data[f.Key]!))
                : f.Conditions!.Items!.Any(c => Matches(c, data[f.Key]!)),
            _ => throw new NotSupportedException(filter.GetType().Name)
        };

        return filter.Negated ? !result : result;
    }

    private static bool Matches(ValueCondition condition,
        DataValue data)
    {
        var result = condition switch
        {
            EqualsCondition c => JToken.DeepEquals(JToken.FromObject(c.Value!.Value!), JToken.FromObject(data.Value!)),
            ContainsCondition { ObjectFilter: { } objectFilter } => data.ValueAsListOf<DataObject>()!.Any(v =>
                objectFilter.Conditions!.All(c => Matches(c, v))),
            ContainsCondition c => data.IsCollection
                ? data.ValueAsListOf<string>()!.Contains(c.Value!.ValueAs<string>())
                : data.ValueAs<string>()!.Contains(c.Value!.ValueAs<string>()!, StringComparison.Ordinal),
            _ => throw new NotSupportedException(condition.GetType().Name)
        };

        return condition.Negated ? !result : result;
    }

    private static bool Matches(ObjectValueCondition condition,
        DataObject data)
    {
        var value = data.Data[condition.Key]!.ValueAs<double>();
        var result = condition switch
        {
            ObjectValueLessThanCondition c => value < c.Value,
            ObjectValueGreaterThanCondition c => value > c.Value,
            ObjectValueInRangeCondition c => value >= c.Range!.LowerBoundInclusive &&
                                             value <= c.Range.UpperBoundInclusive,
            _ => throw new NotSupportedException(condition.GetType().Name)
        };

        return condition.Negated ? !result : result;
    }
}
