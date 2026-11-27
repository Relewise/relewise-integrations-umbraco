using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.Search;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Search.Configuration;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class ExportSystemRegistrationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BootstrapSelectsOneSystemAndOnlyRegistersSearchWhenEnabled(int mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Mock.Of<IHostApplicationLifetime>());
        services.AddSingleton(Mock.Of<IBackgroundTaskQueue>());
        services.AddSingleton(Mock.Of<ICoreScopeProvider>());
        var builder = new TestBuilder(services);
        if (mode >= 0)
            new SearchComposer().Compose(builder);
        int calls = 0;
        builder.AddRelewise(options =>
        {
            calls++;
            if (mode == 1)
                options.ConfigureSearch();
            if (mode >= 2)
            {
                options.ConfigureSearch(search =>
                {
                    search.AddContentProvider("articles");
                    if (mode == 3)
                        search.AddProductProvider("catalogue");
                });
            }

            if (mode == 3)
                options.MapProduct("product");
            options.MapContent("article"); // Selection may precede mappings; validation runs after all callbacks.
        });

        if (mode <= 0)
        {
            services.AddSingleton<UmbracoSearchExportSystem>(_ => throw new InvalidOperationException("Wrong system"));
        }
        else
        {
            services.AddSingleton<NotificationExportSystem>(_ => throw new InvalidOperationException("Wrong system"));
        }

        using ServiceProvider provider =
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IExportSystem selected = Assert.Single(provider.GetServices<IExportSystem>());
        Assert.Same(selected, provider.GetRequiredService<IExportSystem>());
        Assert.Equal(mode <= 0, selected.UsesContentNotifications);
        IndexOptions indexes = provider.GetRequiredService<IOptions<IndexOptions>>().Value;
        Assert.Equal(mode > 0, indexes.GetIndexRegistration(SearchExportOperations.IndexAlias) != null);
        Assert.Equal(mode >= 2, indexes.GetIndexRegistration("articles") != null);
        Assert.Equal(mode == 3, indexes.GetIndexRegistration("catalogue") != null);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SearchActivationWithoutComposersExplainsHowToRegisterThePackage()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        new TestBuilder(services).AddRelewise(options =>
        {
            options.MapContent("article");
            options.ConfigureSearch();
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IExportSystem>());
        Assert.Contains("AddComposers()", error.Message);
    }

    [Fact]
    public void SearchPackageDoesNotRequireCoreToBeConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IHostApplicationLifetime>());
        services.AddSingleton(Mock.Of<IBackgroundTaskQueue>());
        services.AddScoped(_ => Mock.Of<IPublishedContentChangeStrategy>());
        new SearchComposer().Compose(new TestBuilder(services));
        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        IndexOptions indexes = provider.GetRequiredService<IOptions<IndexOptions>>().Value;
        Assert.Null(indexes.GetIndexRegistration(SearchExportOperations.IndexAlias));
        Assert.Null(provider.GetService<IExportSystem>());
    }

    [Fact]
    public void ConfigurationCallbacksRunOnceAfterTheContainerIsBuilt()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        int calls = 0;
        new TestBuilder(services).AddRelewise((options, provider) =>
        {
            calls++;
            Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
            options.MapContent("article");
            options.ConfigureSearch();
        });
        services.AddSingleton(TimeProvider.System);
        Assert.Equal(0, calls);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal(0, calls);

        provider.GetRequiredService<RelewiseUmbracoConfiguration>();
        provider.GetRequiredService<RelewiseUmbracoConfiguration>();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SearchImplementationLivesInADependentAssembly()
    {
        var core = typeof(RelewiseUmbracoConfiguration).Assembly;
        var search = typeof(SearchConfigurationExtensions).Assembly;
        Assert.NotEqual(core, search);
        Assert.DoesNotContain(core.GetReferencedAssemblies(), name => name.Name == search.GetName().Name);
        Assert.Contains(search.GetReferencedAssemblies(), name => name.Name == core.GetName().Name);
    }

    [Fact]
    public void RepeatedBootstrapCallsRetainOneExportSystemAndOneContentNotificationHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Mock.Of<IHostApplicationLifetime>());
        services.AddSingleton(Mock.Of<IBackgroundTaskQueue>());
        services.AddSingleton(Mock.Of<ICoreScopeProvider>());
        var builder = new TestBuilder(services);
        new SearchComposer().Compose(builder);
        builder.AddRelewise(options => options.MapContent("article"));
        builder.AddRelewise(options => options.ConfigureSearch());
        using ServiceProvider provider =
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Assert.IsType<UmbracoSearchExportSystem>(Assert.Single(provider.GetServices<IExportSystem>()));
        Assert.Single(provider.GetServices<INotificationHandler<ContentPublishedNotification>>());
    }

    [Fact]
    public void RepeatedConfigureSearchCallsAcrossCallbacksAreRejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => options.ConfigureSearch());
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => options.ConfigureSearch());
        using ServiceProvider provider = services.BuildServiceProvider();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new RelewiseUmbracoConfiguration(provider));
        Assert.Contains("ConfigureSearch", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyTheNotificationSystemReceivesContentNotifications(bool notifications)
    {
        var exports = new Mock<IExportSystem>();
        exports.SetupGet(value => value.UsesContentNotifications).Returns(notifications);
        ExportChange[]? received = null;
        exports.Setup(value => value.EnqueueChanges(It.IsAny<IEnumerable<ExportChange>>()))
            .Callback<IEnumerable<ExportChange>>(items => received = [.. items]);
        var content = new Mock<IContent>();
        var key = Guid.NewGuid();
        content.SetupGet(value => value.Key).Returns(key);
        content.SetupGet(value => value.Published).Returns(true);
        var handler = new ContentExportNotificationHandler(exports.Object, Mock.Of<ICoreScopeProvider>());
        handler.Handle(new ContentPublishedNotification(content.Object, new EventMessages()));
        if (notifications)
        {
            Assert.Equal(key, Assert.Single(received!).Id);
        }
        else
        {
            Assert.Null(received);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ContentExportsWaitForCommitAndDiscardRolledBackChanges(bool completed)
    {
        var transaction = new TestScopeContext();
        var scopes = new Mock<ICoreScopeProvider>();
        scopes.SetupGet(value => value.Context).Returns(transaction);
        var exports = new Mock<IExportSystem>();
        exports.SetupGet(value => value.UsesContentNotifications).Returns(true);
        var key = Guid.NewGuid();
        var content = new Mock<IContent>();
        content.SetupGet(value => value.Key).Returns(key);
        content.SetupGet(value => value.Published).Returns(true);
        var handler = new ContentExportNotificationHandler(exports.Object, scopes.Object);

        handler.Handle(new ContentPublishedNotification(content.Object, new EventMessages()));
        exports.Verify(value => value.EnqueueChanges(It.IsAny<IEnumerable<ExportChange>>()), Times.Never);
        transaction.ScopeExit(completed);
        exports.Verify(
            value => value.EnqueueChanges(It.Is<IEnumerable<ExportChange>>(changes => changes.Single().Id == key)),
            completed ? Times.Once() : Times.Never());
    }

    private sealed class TestBuilder(IServiceCollection services) : IUmbracoBuilder
    {
        private readonly Dictionary<Type, ICollectionBuilder> _collections = new();
        public IServiceCollection Services => services;
        public IConfiguration Config => throw new NotSupportedException();
        public TypeLoader TypeLoader => throw new NotSupportedException();
        public ILoggerFactory BuilderLoggerFactory => throw new NotSupportedException();
        public IProfiler Profiler => throw new NotSupportedException();
        public AppCaches AppCaches => throw new NotSupportedException();

        public TBuilder WithCollectionBuilder<TBuilder>() where TBuilder : ICollectionBuilder
        {
            if (!_collections.TryGetValue(typeof(TBuilder), out ICollectionBuilder? builder))
            {
                _collections.Add(typeof(TBuilder), builder = Activator.CreateInstance<TBuilder>());
            }

            return (TBuilder)builder;
        }

        public void Build() => throw new NotSupportedException();
    }
}
