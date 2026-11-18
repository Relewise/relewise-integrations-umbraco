#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Helpers to register Relewise-helpers in the IoC
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add a Property Value Converter, that can map a Umbraco property to a Relewise Data Value
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [Obsolete("Use AddAsyncValueConverter with IAsyncRelewisePropertyValueConverter instead.")]
    public static IServiceCollection AddValueConverter<T>(this IServiceCollection services) where T : class, IRelewisePropertyValueConverter
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        var descriptor = new ServiceDescriptor(typeof(IRelewisePropertyValueConverter), typeof(T), ServiceLifetime.Singleton);

        services.TryAddEnumerable(descriptor);

        return services;
    }
    /// <summary>Register an asynchronous Data converter in the export operation's service scope.</summary>
    /// <typeparam name="T">The contextual converter implementation.</typeparam>
    /// <param name="services">Application services.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAsyncValueConverter<T>(this IServiceCollection services) where T : class, IAsyncRelewisePropertyValueConverter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAsyncRelewisePropertyValueConverter, T>());
        return services;
    }
}
