using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco;

// Umbraco owns the worker. Only immutable change records/identities are retained between callbacks.
internal sealed class EntityIndexingQueue(
    IEntityIndexingOperations indexing,
    IBackgroundTaskQueue queue,
    IHostApplicationLifetime lifetime,
    ILogger<EntityIndexingQueue> logger)
{
    internal const int BatchSize = 100;
    private readonly object _sync = new();
    private readonly Queue<Work> _pending = new();
    private readonly SemaphoreSlim _processing = new(1, 1);
    private readonly AsyncLocal<Execution?> _execution = new();
    private bool _flushQueued;

    internal void EnqueueChanges(IEnumerable<ContentChange> changes)
    {
        ContentChange[][] batches = changes.Chunk(BatchSize).ToArray();
        lock (_sync)
        {
            foreach (ContentChange[] batch in batches)
            {
                _pending.Enqueue(new Changes(batch));
            }
        }
        ScheduleFlush();
    }

    internal void EnqueueDeletion(DeletedEntity[] entities, Guid[] parents)
    {
        var snapshot = new Deletion(entities.ToArray(), parents.ToArray());
        lock (_sync)
        {
            _pending.Enqueue(snapshot);
        }
        ScheduleFlush();
    }

    internal Task ExportAsync(IEnumerable<Guid> keys, CancellationToken token = default,
        ContentUpdate.UpdateKind? updateKind = null, long? version = null)
    {
        Guid[] identities = keys.ToArray();
        return ExecuteAsync(cancellation => indexing.ExportAsync(identities, cancellation, updateKind, version), token);
    }

    internal Task RebuildAsync(bool permanentlyDelete = false, CancellationToken token = default)
        => ExecuteAsync(cancellation => indexing.RebuildAsync(permanentlyDelete, cancellation), token);

    private async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken token)
    {
        if (_execution.Value is { Active: true })
        {
            throw new InvalidOperationException("An explicit export or rebuild cannot be started recursively from an indexing mapper or converter.");
        }

        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.ApplicationStopping);
        waiting.Token.ThrowIfCancellationRequested();
        var boundary = new Boundary(action, token);
        lock (_sync)
        {
            _pending.Enqueue(boundary);
        }
        // A rebuild may itself run on Umbraco's worker: execute inline rather than awaiting that same queue.
        try
        {
            await _processing.WaitAsync(waiting.Token);
            try
            {
                while (!boundary.Completion.Task.IsCompleted)
                {
                    // Finish the current operation's scope before returning, but leave other callers' queued work intact.
                    waiting.Token.ThrowIfCancellationRequested();
                    await ProcessAsync(TakeNext() ?? throw new InvalidOperationException("The queued indexing operation is missing."), lifetime.ApplicationStopping);
                }
            }
            finally
            {
                _processing.Release();
            }
            await boundary.Completion.Task;
        }
        catch (OperationCanceledException) when (waiting.IsCancellationRequested)
        {
            boundary.Completion.TrySetCanceled(waiting.Token);
            throw;
        }
    }

    // There is at most one outstanding flush in Umbraco's queue. Do not invoke the queue under _sync.
    private void ScheduleFlush()
    {
        lock (_sync)
        {
            if (_flushQueued || _pending.Count == 0)
            {
                return;
            }

            _flushQueued = true;
        }
        try
        {
            queue.QueueBackgroundWorkItem(FlushAsync);
        }
        catch
        {
            lock (_sync)
            {
                _flushQueued = false;
            }

            throw;
        }
    }

    private async Task FlushAsync(CancellationToken token)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.ApplicationStopping);
        token = stopping.Token;
        bool entered = false;
        try
        {
            await _processing.WaitAsync(token);
            entered = true;
            Work? work = TakeNext();
            if (work != null)
            {
                await ProcessAsync(work, token);
            }
        }
        finally
        {
            if (entered)
            {
                _processing.Release();
            }

            lock (_sync)
            {
                _flushQueued = false;
            }

            if (!token.IsCancellationRequested)
            {
                ScheduleFlush();
            }
        }
    }

    private Work? TakeNext()
    {
        lock (_sync)
        {
            if (!_pending.TryDequeue(out Work? work))
            {
                return null;
            }

            if (work is not Changes first)
            {
                return work;
            }

            var combined = new List<ContentChange>(first.Items);
            while (_pending.TryPeek(out Work? next) && next is Changes changes && combined.Count + changes.Items.Length <= BatchSize)
            {
                _pending.Dequeue();
                combined.AddRange(changes.Items);
            }
            return new Changes(combined.ToArray());
        }
    }

    private async Task ProcessAsync(Work work, CancellationToken token)
    {
        Execution? previous = _execution.Value;
        var current = new Execution();
        _execution.Value = current;
        try
        {
            token.ThrowIfCancellationRequested();
            switch (work)
            {
                case Changes changes:
                    await indexing.ApplyChangesAsync(changes.Items, token);
                    break;
                case Deletion deletion:
                    await indexing.DeleteAsync(deletion.Entities, token);
                    if (deletion.Parents.Length > 0)
                    {
                        await indexing.ExportAsync(deletion.Parents, token);
                    }

                    break;
                case Boundary boundary:
                    if (boundary.Completion.Task.IsCompleted)
                    {
                        break; // A waiting caller may have cancelled.
                    }

                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, boundary.Token))
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        await boundary.Action(linked.Token);
                    }
                    boundary.Completion.TrySetResult();
                    break;
            }
        }
        catch (Exception error)
        {
            bool cancelled = error is OperationCanceledException && (token.IsCancellationRequested
                || work is Boundary requested && requested.Token.IsCancellationRequested);
            if (work is Boundary boundary)
            {
                if (cancelled)
                {
                    boundary.Completion.TrySetCanceled(boundary.Token.IsCancellationRequested ? boundary.Token : token);
                }
                else
                {
                    boundary.Completion.TrySetException(error);
                    _ = boundary.Completion.Task.Exception; // Observe even if its caller cancelled while waiting for the writer.
                }
            }
            if (!cancelled)
            {
                logger.LogError(error, "Relewise indexing work failed. A full export may be needed to reconcile published content.");
            }
        }
        finally
        {
            current.Active = false;
            _execution.Value = previous;
        }
    }

    private sealed class Execution
    {
        internal volatile bool Active = true;
    }

    private abstract record Work;
    private sealed record Changes(ContentChange[] Items) : Work;
    private sealed record Deletion(DeletedEntity[] Entities, Guid[] Parents) : Work;
    private sealed record Boundary(Func<CancellationToken, Task> Action, CancellationToken Token) : Work
    {
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
