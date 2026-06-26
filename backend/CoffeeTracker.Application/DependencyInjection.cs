using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeTracker.Application;

public static class DependencyInjection
{
    /// <summary>Registers application-layer use cases (driving ports).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICoffeeCatalogService, CoffeeCatalogService>();
        return services;
    }
}
