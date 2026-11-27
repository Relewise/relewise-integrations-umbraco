#pragma warning disable CS0618 // Verify compatibility with existing converter implementations.

using System.Globalization;
using Moq;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

internal static class ValueConverterTestSupport
{
    internal static IPublishedProperty Property(
        string alias,
        string editor,
        Func<string?, object> value)
    {
        var type = new Mock<IPublishedPropertyType>();

        type.SetupGet(publishedProperty => publishedProperty.EditorAlias).Returns(editor);
        type.SetupGet(publishedProperty => publishedProperty.Variations)
            .Returns(ContentVariation.Culture);

        var property = new Mock<IPublishedProperty>();

        property.SetupGet(publishedProperty => publishedProperty.Alias).Returns(alias);
        property.SetupGet(publishedProperty => publishedProperty.PropertyType).Returns(type.Object);
        property.Setup(publishedProperty => publishedProperty.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string? culture, string? _) => value(culture));

        return property.Object;
    }

    private static void Add(RelewisePropertyConverterContext context)
    {
        object? value = context.Property.GetValue(context.Culture);

        context.Add(
            context.Property.Alias,
            value is string text
                ? new DataValue(text)
                : new DataValue(Convert.ToDouble(value, CultureInfo.InvariantCulture)));
    }

    internal sealed class LegacyConverter : IRelewisePropertyValueConverter
    {
        public bool CanHandle(RelewisePropertyConverterContext context)
        {
            return true;
        }

        public void Convert(RelewisePropertyConverterContext context)
        {
            Add(context);
        }
    }

    internal sealed class AsyncConverter : IAsyncRelewisePropertyValueConverter
    {
        public bool CanHandle(RelewisePropertyConverterContext context)
        {
            return true;
        }

        public async ValueTask ConvertAsync(
            RelewisePropertyConverterContext context,
            CancellationToken token)
        {
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            Add(context);
        }
    }

    internal sealed class ScopedConverter : IAsyncRelewisePropertyValueConverter, IDisposable
    {
        public bool Disposed { get; private set; }

        public bool CanHandle(RelewisePropertyConverterContext context)
        {
            return true;
        }

        public async ValueTask ConvertAsync(
            RelewisePropertyConverterContext context,
            CancellationToken token)
        {
            await Task.Delay(1, token);
            Assert.NotNull(context.Services);
            Assert.False(Disposed);
            context.Add(context.Property.Alias, "override");
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    internal sealed class DualConverter : IAsyncRelewisePropertyValueConverter, IRelewisePropertyValueConverter
    {
        public bool CanHandle(RelewisePropertyConverterContext context)
        {
            return true;
        }

        public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            context.Add(context.Property.Alias, "async");

            return ValueTask.CompletedTask;
        }

        public void Convert(RelewisePropertyConverterContext context)
        {
            throw new InvalidOperationException("The synchronous contract must not run.");
        }
    }
}
