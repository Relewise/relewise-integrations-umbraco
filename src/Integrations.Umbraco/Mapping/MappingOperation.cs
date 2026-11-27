using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class MappingOperation(
    IServiceProvider services,
    CancellationToken token) : IAsyncDisposable
{
    private readonly Dictionary<Type, object> _instances = new();
    internal IServiceProvider Services { get; } = services;
    internal CancellationToken CancellationToken { get; } = token;
    internal bool FullRebuild { get; init; }

    internal object Resolve(Type type)
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

    internal T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    private Task<string>? _defaultCulture;

    internal async Task<string> DefaultCultureAsync(string[] publishedCultures)
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
