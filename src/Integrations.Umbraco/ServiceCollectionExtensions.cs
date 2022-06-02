using System;
using Microsoft.Extensions.DependencyInjection;

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
    public static IServiceCollection AddValueConverter<T>(this IServiceCollection services) where T : class, IRelewisePropertyValueConverter
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        var descriptor = new ServiceDescriptor(typeof(IRelewisePropertyValueConverter), typeof(T), ServiceLifetime.Singleton);

        if (!services.Contains(descriptor))
        {
            services.Add(descriptor);
        }

        return services;
    }
}