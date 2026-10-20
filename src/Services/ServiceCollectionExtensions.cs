using Microsoft.Extensions.DependencyInjection;

namespace FlintSoft.Components;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlintSoftComponents(this IServiceCollection services)
    {
        services.AddScoped<FsToastService>();
        services.AddScoped<FsModalCoordinator>();
        return services;
    }
}
