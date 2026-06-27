using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoffeeTracker.Application;

public static class DependencyInjection
{
    /// <summary>Registers application-layer use cases (driving ports).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICoffeeCatalogService, CoffeeCatalogService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ICoffeeScanService, CoffeeScanService>();
        services.AddScoped<IPhotoAdminService, PhotoAdminService>();
        services.AddSingleton<ICoffeeLabelParser, CoffeeLabelParser>();
        return services;
    }
}
