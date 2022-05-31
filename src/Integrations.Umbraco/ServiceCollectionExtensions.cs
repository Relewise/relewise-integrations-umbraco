using System;
using Microsoft.Extensions.DependencyInjection;

namespace Relewise.Integrations.Umbraco;

public static class ServiceCollectionExtensions
{
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