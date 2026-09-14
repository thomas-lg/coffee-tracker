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
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountSettingsService, AccountSettingsService>();
        services.AddScoped<IExternalSignInService, ExternalSignInService>();
        services.AddScoped<ICoffeeCatalogService, CoffeeCatalogService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ICoffeeScanService, CoffeeScanService>();
        services.AddScoped<IPhotoAdminService, PhotoAdminService>();
        services.AddScoped<IBackupService, BackupService>();
        // No gate here: the engine reports its own with every read, because the number is
        // a property of how it scores rather than of the bag.
        services.AddSingleton<ICoffeeLabelParser, CoffeeLabelParser>();
        services.AddScoped<IScanSettingsService, ScanSettingsService>();
        return services;
    }
}
