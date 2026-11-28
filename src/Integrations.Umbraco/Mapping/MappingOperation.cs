using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class MappingOperation(
    IServiceProvider services,
    CancellationToken token) : IAsyncDisposable
{
    private readonly Dictionary<Type, object> _instances = new();
    private readonly Dictionary<string, Guid> _claims = new(StringComparer.Ordinal);
    public IServiceProvider Services { get; } = services;
    public CancellationToken CancellationToken { get; } = token;
    public bool FullRebuild { get; init; }

    public void ClaimIdentity(Type entityType, string id, Guid nodeKey)
    {
        string claim = entityType.Name + ":" +
                       (entityType == typeof(Brand) ? id.ToLowerInvariant() : id);
        if (_claims.TryGetValue(claim, out Guid node) && node != nodeKey)
        {
            throw new InvalidOperationException(
                $"Multiple documents export the same {entityType.Name} ID '{id}'.");
        }

        // Retain identities across streamed batches without retaining mapped entities or tracking payloads.
        _claims[claim] = nodeKey;
    }

    public object Resolve(Type type)
    {
        // DI owns registered services; this operation disposes only instances it creates itself.
        object? registered = Services.GetService(type);

        if (registered != null)
        {
            return registered;
        }

        if (_instances.TryGetValue(type, out object? instance))
        {
            return instance;
        }

        instance = ActivatorUtilities.CreateInstance(Services, type);
        _instances.Add(type, instance);

        return instance;
    }

    public T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    private Task<string>? _defaultCulture;

    public async Task<string> DefaultCultureAsync(string[] publishedCultures)
    {
        string culture = await (_defaultCulture ??= Services
            .GetRequiredService<ILanguageService>()
            .GetDefaultIsoCodeAsync());

        return publishedCultures.FirstOrDefault(value => value.Equals(culture, StringComparison.OrdinalIgnoreCase))
               ?? publishedCultures.Order(StringComparer.OrdinalIgnoreCase).First();
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception>? errors = null;

        foreach (object instance in _instances.Values.Reverse())
        {
            try
            {
                if (instance is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
        }

        _instances.Clear();

        if (errors != null)
        {
            throw new AggregateException("Mapping resources could not all be disposed.", errors);
        }
    }
}
