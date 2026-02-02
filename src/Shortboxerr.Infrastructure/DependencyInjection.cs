using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Providers;
using Shortboxerr.Infrastructure.Services;

namespace Shortboxerr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ShortboxerrDbContext>(options =>
            options.UseSqlite(connectionString));

        // Services
        services.AddSingleton<IFilenameParser, FilenameParser>();
        services.AddScoped<IStagingService, StagingService>();
        services.AddSingleton<IDecisionEngine, DecisionEngine>();

        // Provider system
        services.AddSingleton<IProviderFactory, ProviderFactory>();
        services.AddScoped<IProviderManager, ProviderManager>();

        // Settings (can be overridden via configuration)
        services.Configure<DecisionEngineSettings>(options =>
        {
            // Defaults are set in the class, but can be bound from config here
        });

        return services;
    }
}
