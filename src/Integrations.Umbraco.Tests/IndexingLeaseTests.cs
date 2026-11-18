using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Relewise.Integrations.Umbraco.Tests;

public class IndexingLeaseTests
{
    [Fact]
    public async Task LeaseSerializesWritersWithoutKeepingDatabaseScopesOpen()
    {
        var values = new ConcurrentDictionary<string, string>();
        var store = new Mock<IKeyValueService>();
        store.Setup(service => service.GetValue(It.IsAny<string>())).Returns((string key) => values.GetValueOrDefault(key));
        store.Setup(service => service.SetValue(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((key, value) => values[key] = value);
        var scopes = new Mock<IScopeProvider> { DefaultValue = DefaultValue.Mock };
        int open = 0;
        scopes.Setup(provider => provider.CreateScope()).Returns(() =>
        {
            Interlocked.Increment(ref open);
            var scope = new Mock<IScope>(); scope.Setup(value => value.Dispose()).Callback(() => Interlocked.Decrement(ref open));
            return scope.Object;
        });
        var services = new ServiceCollection(); services.AddSingleton(store.Object); services.AddSingleton(scopes.Object);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var factory = provider.GetRequiredService<IServiceScopeFactory>();
        Guid dataset = Guid.NewGuid();
        var first = await IndexingLease.AcquireAsync(factory, dataset, default);
        Assert.Equal(0, open);
        var secondTask = IndexingLease.AcquireAsync(factory, dataset, default);
        Assert.False(secondTask.IsCompleted);
        Assert.Equal(0, open);
        await first.DisposeAsync();
        await using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, open);
        second.EnsureOwned();
        Assert.Equal(0, open);
    }
}
