#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters.

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
    /// Register an asynchronous Data converter in the export operation's service scope.
    /// </summary>
    /// <typeparam name="T">The contextual converter implementation.</typeparam>
    /// <param name="services">Application services.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddValueConverter<T>(this IServiceCollection services)
        where T : class, IAsyncRelewisePropertyValueConverter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAsyncRelewisePropertyValueConverter, T>());

        return services;
    }

    /// <summary>
    /// Register a legacy synchronous Data converter with its original singleton lifetime.
    /// </summary>
    /// <typeparam name="T">The legacy converter implementation.</typeparam>
    /// <param name="services">Application services.</param>
    /// <returns>The service collection.</returns>
    [Obsolete(
        "Implement IAsyncRelewisePropertyValueConverter and register with AddValueConverter instead. " +
        "Legacy converter support will be removed in the next major version.")]
    public static IServiceCollection AddLegacyValueConverter<T>(this IServiceCollection services)
        where T : class, IRelewisePropertyValueConverter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRelewisePropertyValueConverter, T>());

        return services;
    }
}
