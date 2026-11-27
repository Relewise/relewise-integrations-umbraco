#pragma warning disable CS0618 // Exercise the retained public API and custom implementations written against it.

using Newtonsoft.Json;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco.Tests.Compatibility;

public class UserLocatorCompatibilityTests
{
    [Fact]
    public async Task ExistingUserLocatorSupportsTheAsyncEntryPointWithoutResolvingTwice()
    {
        var completion = new TaskCompletionSource<User>(TaskCreationOptions.RunContinuationsAsynchronously);
        var implementation = new LegacyUserLocator(completion.Task);
        IRelewiseUserLocator locator = implementation;

        Task<User> pending = locator.GetUserAsync();

        Assert.False(pending.IsCompleted);
        Assert.Equal(1, implementation.Calls);

        User expected = User.ByTemporaryId("existing-browser");
        completion.SetResult(expected);

        Assert.Same(expected, await pending);
    }

    [Fact]
    public async Task DefaultUserLocatorRetainsItsLegacyEntryPoint()
    {
        IRelewiseUserLocator locator = new DefaultAnonymousUserLocator();

        Assert.Equal(
            JsonConvert.SerializeObject(await locator.GetUserAsync()),
            JsonConvert.SerializeObject(await locator.GetUser()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncOnlyUserLocatorSupportsBothEntryPoints(bool legacy)
    {
        var completion = new TaskCompletionSource<User>(TaskCreationOptions.RunContinuationsAsynchronously);
        var implementation = new AsyncOnlyUserLocator(completion.Task);
        IRelewiseUserLocator locator = implementation;

        Task<User> pending = legacy ? locator.GetUser() : locator.GetUserAsync();

        Assert.Same(completion.Task, pending);
        Assert.Equal(1, implementation.Calls);

        User expected = User.ByTemporaryId("async-browser");
        completion.SetResult(expected);

        Assert.Same(expected, await pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitAndInheritedLegacyUserLocatorsSupportTheAsyncEntryPoint(bool explicitImplementation)
    {
        User expected = User.ByTemporaryId("existing-browser");
        Task<User> result = Task.FromResult(expected);
        IRelewiseUserLocator locator = explicitImplementation
            ? new ExplicitLegacyUserLocator(result)
            : new InheritedLegacyUserLocator(result);

        Assert.Same(expected, await locator.GetUserAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserLocatorCompatibilityPreservesFailureAndCancellation(bool cancelled)
    {
        var failure = new InvalidOperationException("Identity resolution failed.");
        Task<User> result = cancelled
            ? Task.FromCanceled<User>(new CancellationToken(true))
            : Task.FromException<User>(failure);
        IRelewiseUserLocator oldImplementation = new LegacyUserLocator(result);
        IRelewiseUserLocator newImplementation = new AsyncOnlyUserLocator(result);

        foreach (Task<User> pending in new[] { oldImplementation.GetUserAsync(), newImplementation.GetUser() })
        {
            Assert.Same(result, pending);

            if (cancelled)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => pending));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingUserLocatorImplementationFailsClearlyFromEitherEntryPoint(bool legacy)
    {
        IRelewiseUserLocator locator = new MissingUserLocator();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            legacy ? locator.GetUser() : locator.GetUserAsync());

        Assert.Contains(nameof(MissingUserLocator), error.Message);
        Assert.Contains(nameof(IRelewiseUserLocator.GetUserAsync), error.Message);
        Assert.Contains(nameof(IRelewiseUserLocator.GetUser), error.Message);
    }

    private class LegacyUserLocator(Task<User> result) : IRelewiseUserLocator
    {
        public int Calls { get; private set; }

        public Task<User> GetUser()
        {
            Calls++;

            return result;
        }
    }

    private sealed class AsyncOnlyUserLocator(Task<User> result) : IRelewiseUserLocator
    {
        public int Calls { get; private set; }

        public Task<User> GetUserAsync()
        {
            Calls++;

            return result;
        }
    }

    private sealed class ExplicitLegacyUserLocator(Task<User> result) : IRelewiseUserLocator
    {
        Task<User> IRelewiseUserLocator.GetUser()
        {
            return result;
        }
    }

    private sealed class InheritedLegacyUserLocator(Task<User> result) : LegacyUserLocator(result)
    {
    }

    private sealed class MissingUserLocator : IRelewiseUserLocator
    {
    }
}
