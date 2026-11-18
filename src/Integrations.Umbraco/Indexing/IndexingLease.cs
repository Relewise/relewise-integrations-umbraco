using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Relewise.Integrations.Umbraco;

// Keep database transactions short. In particular, SQLite's write lock must never surround a network call.
internal sealed class IndexingLease : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopes;
    private readonly string _key;
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationTokenSource _operation;
    private Task _renewal = Task.CompletedTask;
    private Exception? _failure;
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);
    private IndexingLease(IServiceScopeFactory scopes, string key, CancellationToken cancellationToken)
    {
        _scopes = scopes; _key = key;
        _operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }
    internal CancellationToken CancellationToken => _operation.Token;
    internal static async Task<IndexingLease> AcquireAsync(IServiceScopeFactory scopes, Guid datasetId, CancellationToken cancellationToken)
    {
        var lease = new IndexingLease(scopes, "Relewise:IndexingLease:" + datasetId.ToString("N"), cancellationToken);
        try
        {
            while (!lease.Change(acquire: true)) await Task.Delay(250, cancellationToken);
            lease._renewal = lease.RenewAsync();
            return lease;
        }
        catch { lease._stop.Dispose(); lease._operation.Dispose(); throw; }
    }
    private bool Change(bool acquire = false, bool release = false)
    {
        using IServiceScope services = _scopes.CreateScope();
        using IScope scope = services.ServiceProvider.GetRequiredService<IScopeProvider>().CreateScope();
        scope.EagerWriteLock(global::Umbraco.Cms.Core.Constants.Locks.KeyValues);
        var store = services.ServiceProvider.GetRequiredService<IKeyValueService>();
        string? value = store.GetValue(_key);
        State? current = string.IsNullOrEmpty(value) ? null : JsonSerializer.Deserialize<State>(value);
        bool owned = current?.Owner == _owner;
        if (!owned && (!acquire || current?.Expires > DateTimeOffset.UtcNow)) { scope.Complete(); return false; }
        store.SetValue(_key, release ? string.Empty : JsonSerializer.Serialize(new State(_owner, DateTimeOffset.UtcNow.Add(Duration))));
        scope.Complete();
        return true;
    }
    private async Task RenewAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), _stop.Token);
                if (!Change()) throw new InvalidOperationException("The Relewise indexing lease was lost.");
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception error) { _failure = error; await _operation.CancelAsync(); }
    }
    internal void EnsureOwned()
    {
        if (_failure != null) throw new InvalidOperationException("The Relewise indexing lease could not be renewed.", _failure);
        _operation.Token.ThrowIfCancellationRequested();
        if (!Change()) throw new InvalidOperationException("The Relewise indexing lease is no longer owned by this operation.");
    }
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _renewal;
        try { Change(release: true); }
        finally { _stop.Dispose(); _operation.Dispose(); }
    }
    private sealed record State(string Owner, DateTimeOffset Expires);
}
