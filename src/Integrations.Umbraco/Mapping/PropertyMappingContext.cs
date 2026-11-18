using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

/// <summary>Read-only source information for conversion of a built-in destination property.</summary>
public class PropertyMappingContext
{
    private readonly MappingOperation _operation;
    internal PropertyMappingContext(IPublishedContent publishedContent, IPublishedElement sourceElement, IPublishedProperty property,
        IReadOnlyList<string> culturesToPublish, string destinationProperty, MappingOperation operation)
    {
        PublishedContent = publishedContent;
        SourceElement = sourceElement;
        Property = property;
        CulturesToPublish = culturesToPublish;
        DestinationProperty = destinationProperty;
        _operation = operation;
    }
    /// <summary>The owning published document, retained when converting nested elements.</summary>
    public IPublishedContent PublishedContent { get; }
    /// <summary>The element containing the source property.</summary>
    public IPublishedElement SourceElement { get; }
    /// <summary>The source property, including its editor and localized values.</summary>
    public IPublishedProperty Property { get; }
    /// <summary>The published cultures to include in the destination value.</summary>
    public IReadOnlyList<string> CulturesToPublish { get; }
    /// <summary>The selected built-in destination property.</summary>
    public string DestinationProperty { get; }
    /// <summary>Services belonging to the current export operation.</summary>
    public IServiceProvider Services => _operation.Services;
    /// <summary>Cancellation of the current export operation.</summary>
    public CancellationToken CancellationToken => _operation.CancellationToken;
    internal T Resolve<T>() where T : class => _operation.Resolve<T>();
}

/// <summary>Converts contextual source data to the selected destination property's type.</summary>
/// <typeparam name="TValue">The destination value type, including its nullable contract.</typeparam>
public interface IPropertyMappingConverter<TValue>
{
    /// <summary>Build the complete destination value, including applicable cultures or currencies.</summary>
    ValueTask<TValue> ConvertAsync(PropertyMappingContext context, CancellationToken cancellationToken);
}

internal sealed class MappingOperation(IServiceProvider services, CancellationToken cancellationToken) : IAsyncDisposable
{
    private readonly Dictionary<Type, object> _instances = new();
    internal IServiceProvider Services { get; } = services;
    internal CancellationToken CancellationToken { get; } = cancellationToken;
    internal object Resolve(Type type)
    {
        object? registered = Services.GetService(type);
        if (registered != null) return registered;
        if (_instances.TryGetValue(type, out object? instance)) return instance;
        instance = ActivatorUtilities.CreateInstance(Services, type);
        _instances.Add(type, instance);
        return instance;
    }
    internal T Resolve<T>() where T : class => (T)Resolve(typeof(T));
    private Task<string>? _defaultCulture;
    internal async Task<string> DefaultCultureAsync(string[] publishedCultures)
    {
        string culture = await (_defaultCulture ??= Services.GetRequiredService<global::Umbraco.Cms.Core.Services.ILanguageService>().GetDefaultIsoCodeAsync());
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
                if (instance is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
                else if (instance is IDisposable disposable) disposable.Dispose();
            }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        _instances.Clear();
        if (errors != null) throw new AggregateException("Mapping resources could not all be disposed.", errors);
    }
}
