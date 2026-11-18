using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Tests;

public class MappingContextTests
{
    [Fact]
    public async Task NestedBlockConversionRetainsOwnerElementScopeAndCancellation()
    {
        var owner = Mock.Of<IPublishedContent>();
        var leaf = Property("body", "Custom.Context", "Nested text");
        var element = new Mock<IPublishedElement>();
        element.SetupGet(value => value.Properties).Returns([leaf]);
        var blocks = new BlockListModel([new BlockListItem(Guid.NewGuid(), element.Object, null, null)]);
        var blockProperty = Property("blocks", "Umbraco.BlockList", blocks);
        var source = new Mock<IPublishedElement>(); source.SetupGet(value => value.Properties).Returns([blockProperty]);
        using var cancellation = new CancellationTokenSource();
        RelewisePropertyConverterContext? observed = null;
        var custom = new Mock<IRelewisePropertyValueConverter>();
        custom.Setup(value => value.CanHandle(It.IsAny<RelewisePropertyConverterContext>())).Returns((RelewisePropertyConverterContext context) => context.Property.Alias == "body");
        custom.Setup(value => value.Convert(It.IsAny<RelewisePropertyConverterContext>())).Callback((RelewisePropertyConverterContext context) =>
        {
            observed = context;
            context.Add("body", "Nested text");
        });
        var services = new ServiceCollection();
        services.AddSingleton<IRelewisePropertyValueConverter, BlockListPropertyValueConverter>();
        services.AddSingleton(custom.Object);
        services.AddSingleton<IRelewisePropertyConverter, RelewisePropertyConverter>();
        services.AddScoped<Marker>();
        await using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = root.CreateAsyncScope();
        Marker marker = scope.ServiceProvider.GetRequiredService<Marker>();
        var result = scope.ServiceProvider.GetRequiredService<IRelewisePropertyConverter>().Convert(owner, source.Object, ["en-US"], scope.ServiceProvider, cancellation.Token);
        Assert.Equal("Nested text", Assert.Single(result["blocks"]!.ValueAsListOf<string>()));
        Assert.NotNull(observed);
        Assert.Same(owner, observed.PublishedContent);
        Assert.Same(element.Object, observed.SourceElement);
        Assert.Same(marker, observed.Services!.GetRequiredService<Marker>());
        Assert.Equal(cancellation.Token, observed.CancellationToken);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<IRelewisePropertyConverter>().Convert(owner, source.Object, ["en-US"], scope.ServiceProvider, cancellation.Token));
    }

    [Fact]
    public async Task FailingDisposalStillReleasesOtherActivatedMappingResources()
    {
        await using var root = new ServiceCollection().BuildServiceProvider();
        var operation = new MappingOperation(root, default);
        var resource = operation.Resolve<Resource>();
        operation.Resolve<FailingResource>();
        await Assert.ThrowsAsync<AggregateException>(() => operation.DisposeAsync().AsTask());
        Assert.True(resource.Disposed);
        await operation.DisposeAsync();
    }

    private static IPublishedProperty Property(string alias, string editor, object value)
    {
        var type = new Mock<IPublishedPropertyType>(); type.SetupGet(property => property.EditorAlias).Returns(editor);
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(property => property.Alias).Returns(alias);
        property.SetupGet(property => property.PropertyType).Returns(type.Object);
        property.Setup(property => property.GetValue(It.IsAny<string?>(), It.IsAny<string?>())).Returns(value);
        return property.Object;
    }
    public sealed class Marker;
    public sealed class Resource : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
    public sealed class FailingResource : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("Expected smoke failure");
    }
}
