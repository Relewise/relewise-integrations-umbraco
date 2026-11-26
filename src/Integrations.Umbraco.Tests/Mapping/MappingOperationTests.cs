using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class MappingOperationTests
{
    [Fact]
    public async Task ActivatedConvertersAndScopedDependenciesAreDisposedByTheirOwners()
    {
        var services = new ServiceCollection();

        services.AddScoped<ScopedDependency>();

        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        ScopedDependency dependency;
        AsyncConverter converter;

        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            dependency = scope.ServiceProvider.GetRequiredService<ScopedDependency>();
            await using (var operation = new MappingOperation(scope.ServiceProvider, default))
            {
                converter = operation.Resolve<AsyncConverter>();
                Assert.Same(converter, operation.Resolve<AsyncConverter>());
                Assert.Same(dependency, converter.Dependency);
            }

            Assert.True(converter.Disposed);
            Assert.False(dependency.Disposed);
        }

        Assert.True(dependency.Disposed);
    }

    public sealed class ScopedDependency : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    public sealed class AsyncConverter(ScopedDependency dependency) : IAsyncDisposable
    {
        public ScopedDependency Dependency { get; } = dependency;
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
