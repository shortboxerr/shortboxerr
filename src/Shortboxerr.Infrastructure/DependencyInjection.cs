using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Caching;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Infrastructure.Caching;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Mylar3Migration;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Ddl;
using Shortboxerr.Infrastructure.Ddl.Resolvers;
using Shortboxerr.Infrastructure.Mylar3Migration;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Providers;
using Shortboxerr.Infrastructure.PullList;
using Shortboxerr.Infrastructure.Services;

namespace Shortboxerr.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="enableDebugMode">When true, enables verbose SQL query logging.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        string connectionString,
        bool enableDebugMode = false)
    {
        services.AddDbContext<ShortboxerrDbContext>((serviceProvider, options) =>
        {
            options.UseSqlite(connectionString);
            
            if (enableDebugMode)
            {
                // Enable detailed query logging in debug mode
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                if (loggerFactory != null)
                {
                    options.UseLoggerFactory(loggerFactory);
                }
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Services
        services.AddSingleton<IFilenameParser, FilenameParser>();
        services.AddScoped<IStagingService, StagingService>();
        services.AddSingleton<IDecisionEngine, DecisionEngine>();
        services.AddScoped<ISettingsService, SettingsService>();

        // DDL services
        services.AddSingleton<IDdlReleaseParser, DdlReleaseParser>();
        services.AddSingleton<IDdlFilter, DdlFilter>();
        services.AddSingleton<IDdlRateLimiter, DdlRateLimiter>();
        services.AddSingleton<IDdlSiteAdapterFactory, DdlSiteAdapterFactory>();
        services.AddSingleton<IDdlSearchService, DdlSearchService>();
        services.AddSingleton<IDownloadHostResolverFactory, DownloadHostResolverFactory>();
        services.AddSingleton<IDdlDownloadService>(sp =>
        {
            var resolverFactory = sp.GetRequiredService<IDownloadHostResolverFactory>();
            var logger = sp.GetService<ILogger<DdlDownloadService>>();
            return new DdlDownloadService(resolverFactory, logger);
        });
        services.AddScoped<IDdlImportService, DdlImportService>();
        services.AddScoped<IMylar3ConfigImporter, Mylar3ConfigImporter>();

        // Provider system
        services.AddSingleton<IProviderFactory, ProviderFactory>();
        services.AddScoped<IProviderManager, ProviderManager>();

        // Memory cache and cache service
        services.AddMemoryCache();
        services.Configure<CacheSettings>(options => { }); // Use defaults, can be overridden
        services.AddSingleton<ICacheService, CacheService>();

        // ComicVine client and services
        services.AddHttpClient<IComicVineClient, ComicVineClient>();
        services.AddScoped<ISeriesMetadataService, SeriesMetadataService>();
        services.AddScoped<IIssueMetadataService, IssueMetadataService>();
        services.AddScoped<IEditionMetadataService, EditionMetadataService>();
        services.AddScoped<IAutoMatchService, AutoMatchService>();
        services.AddScoped<IMetadataRefreshService, MetadataRefreshService>();
        services.AddScoped<IMylar3ComicVineImporter, Mylar3ComicVineImporter>();

        // Migration services
        services.AddScoped<IMylar3MigrationService, Mylar3MigrationService>();

        // Background services
        services.AddHostedService<BackgroundServices.MetadataRefreshBackgroundService>();
        services.AddSingleton<BackgroundServices.ComicVineRefreshBackgroundService>();
        services.AddHostedService(provider => 
            provider.GetRequiredService<BackgroundServices.ComicVineRefreshBackgroundService>());
        services.AddSingleton<BackgroundServices.ReleaseDayBackgroundService>();
        services.AddHostedService(provider => 
            provider.GetRequiredService<BackgroundServices.ReleaseDayBackgroundService>());
        services.AddHostedService<BackgroundServices.HealthCheckBackgroundService>();

        // Cover service
        services.AddHttpClient("CoverDownload");
        services.AddScoped<ICoverService, CoverService>();

        // Pull list service
        services.AddScoped<IPullListService, PullListService>();

        // Notification service
        services.AddScoped<INotificationService, Notifications.NotificationService>();

        // Settings (can be overridden via configuration)
        services.Configure<DecisionEngineSettings>(options =>
        {
            // Defaults are set in the class, but can be bound from config here
        });

        return services;
    }
}
