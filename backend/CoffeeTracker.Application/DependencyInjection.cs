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
        // The confidence gate belongs to whichever engine is registered, so Infrastructure
        // supplies it; absent that, the parser's own default applies.
        services.AddSingleton<ICoffeeLabelParser>(sp =>
            sp.GetService<LabelParserConfidence>() is { Gate: var gate }
                ? new CoffeeLabelParser(gate)
                : new CoffeeLabelParser());
        return services;
    }
}
