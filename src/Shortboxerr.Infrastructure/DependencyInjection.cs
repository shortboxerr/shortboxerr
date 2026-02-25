using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Caching;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Infrastructure.Caching;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Mylar3Migration;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;
using Shortboxerr.Core.Notifications;
using Shortboxerr.Core.Nzb;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Ddl;
using Shortboxerr.Infrastructure.Ddl.Resolvers;
using Shortboxerr.Infrastructure.Mylar3Migration;
using Shortboxerr.Infrastructure.Notifications;
using Shortboxerr.Infrastructure.Nzb;
using Shortboxerr.Infrastructure.Persistence;
using Shortboxerr.Infrastructure.Providers;
using Shortboxerr.Infrastructure.PullList;
using Shortboxerr.Infrastructure.Search;
using Shortboxerr.Infrastructure.Services;
using Shortboxerr.Core.Activity;
using Shortboxerr.Core.Search;
using Shortboxerr.Infrastructure.Activity;
using Shortboxerr.Infrastructure.Http;

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

        // Configure default User-Agent for all HttpClient instances
        // This ensures external sites receive a proper User-Agent header
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpClientActions.Add(client =>
            {
                if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(HttpClientDefaults.UserAgent);
                }
            });
        });

        // Services
        services.AddSingleton<IFilenameParser, FilenameParser>();
        services.AddScoped<IStagingService, StagingService>();
        services.AddSingleton<IDecisionEngine, DecisionEngine>();
        services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ISetupStatusService, SetupStatusService>();
        services.AddScoped<ISearchSettingsService, SearchSettingsService>();
        services.AddScoped<ISearchResultScorer, SearchResultScorer>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddSingleton<IArchiveExtractor, ArchiveExtractor>();

        // DDL services
        services.AddSingleton<IDdlReleaseParser, DdlReleaseParser>();
        services.AddSingleton<IDdlFilter, DdlFilter>();
        services.AddSingleton<IDdlRateLimiter, DdlRateLimiter>();
        services.AddSingleton<IDdlSiteAdapterFactory, DdlSiteAdapterFactory>();
        services.AddSingleton<IDdlSearchService, DdlSearchService>();
        
        // Site health monitoring service (also runs as hosted service)
        services.AddSingleton<SiteHealthService>();
        services.AddSingleton<ISiteHealthService>(sp => sp.GetRequiredService<SiteHealthService>());
        services.AddHostedService(sp => sp.GetRequiredService<SiteHealthService>());
        services.AddSingleton<IDownloadHostResolverFactory, DownloadHostResolverFactory>();
        services.AddSingleton<IHostBlacklistService, HostBlacklistService>();
        services.AddHttpClient<IRssFeedService, RssFeedService>();
        services.AddSingleton<IDdlDownloadService>(sp =>
        {
            var resolverFactory = sp.GetRequiredService<IDownloadHostResolverFactory>();
            var blacklistService = sp.GetRequiredService<IHostBlacklistService>();
            var logger = sp.GetService<ILogger<DdlDownloadService>>();
            return new DdlDownloadService(resolverFactory, blacklistService, logger);
        });
        services.AddScoped<IDdlImportService, DdlImportService>();
        services.AddScoped<IMylar3ConfigImporter, Mylar3ConfigImporter>();

        // Provider system
        services.AddSingleton<IProviderFactory, ProviderFactory>();
        services.AddScoped<IProviderManager, ProviderManager>();
        services.AddScoped<IDownloadClientHealthService, DownloadClientHealthService>();

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
        services.AddScoped<IVariantCoverService, VariantCoverService>();

        // Migration services
        services.AddScoped<IMylar3MigrationService, Mylar3MigrationService>();

        // Background services
        services.AddHostedService<BackgroundServices.MetadataRefreshBackgroundService>();
        services.AddSingleton<BackgroundServices.DiscoveryRefreshBackgroundService>();
        services.AddHostedService(provider => 
            provider.GetRequiredService<BackgroundServices.DiscoveryRefreshBackgroundService>());
        services.AddSingleton<BackgroundServices.DiscoveryCoverEnrichmentService>();
        services.AddHostedService(provider =>
            provider.GetRequiredService<BackgroundServices.DiscoveryCoverEnrichmentService>());
        services.AddSingleton<BackgroundServices.UpcomingReleasesEnrichmentService>();
        services.AddHostedService(provider =>
            provider.GetRequiredService<BackgroundServices.UpcomingReleasesEnrichmentService>());
        services.AddSingleton<BackgroundServices.ReleaseDayBackgroundService>();
        services.AddHostedService(provider => 
            provider.GetRequiredService<BackgroundServices.ReleaseDayBackgroundService>());
        services.AddHostedService<BackgroundServices.HealthCheckBackgroundService>();

        // Cover service
        services.AddHttpClient("CoverDownload");
        services.AddScoped<ICoverService, CoverService>();

        // Pull list service
        services.AddScoped<IPullListService, PullListService>();

        // WalkSoftly client for pull list data (Mylar3 parity)
        services.AddHttpClient<Core.WalkSoftly.IWalkSoftlyClient, WalkSoftly.WalkSoftlyClient>();

        // Metron client for cover image fallback (official API with ComicVine ID mapping)
        // MetronClient loads settings from ISettingsService, no IOptions configuration needed
        services.AddHttpClient<Core.Metron.IMetronClient, Metron.MetronClient>();

        // Cover fallback service for enrichment when ComicVine doesn't have issue covers
        services.AddScoped<Core.Services.ICoverFallbackService, Services.CoverFallbackService>();

        // NZB/Usenet services
        services.AddHttpClient<INewznabClient, NewznabClient>();
        services.AddScoped<INzbIndexerProvider, NzbIndexerProvider>();
        services.AddHttpClient<ISabnzbdClient, SabnzbdClient>();
        services.AddSingleton<INzbReleaseParser, NzbReleaseParser>();
        services.AddSingleton<INzbFilterService, NzbFilterService>();
        services.AddScoped<INzbImportService, NzbImportService>();
        services.AddHostedService<BackgroundServices.NzbImportBackgroundService>();

        // Indexer health monitoring
        services.AddScoped<IIndexerHealthService, IndexerHealthService>();
        services.AddSingleton<BackgroundServices.IndexerHealthBackgroundService>();
        services.AddHostedService(provider =>
            provider.GetRequiredService<BackgroundServices.IndexerHealthBackgroundService>());

        // Auto-search services
        services.AddScoped<IAutoSearchService, AutoSearchService>();
        services.AddSingleton<BackgroundServices.AutoSearchBackgroundService>();
        services.AddHostedService(provider => 
            provider.GetRequiredService<BackgroundServices.AutoSearchBackgroundService>());

        // Cover cache cleanup background service
        services.AddSingleton<BackgroundServices.CoverCacheCleanupBackgroundService>();
        services.AddHostedService(provider =>
            provider.GetRequiredService<BackgroundServices.CoverCacheCleanupBackgroundService>());

        // Log compression background service
        services.AddSingleton<BackgroundServices.LogCompressionBackgroundService>();
        services.AddHostedService(provider =>
            provider.GetRequiredService<BackgroundServices.LogCompressionBackgroundService>());

        // Notification services
        services.AddScoped<INotificationService, NotificationService>();
        services.AddHttpClient<INotificationProvider, WebhookNotificationProvider>();
        services.AddSingleton<INotificationProvider, WebhookNotificationProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetService<ILogger<WebhookNotificationProvider>>();
            return new WebhookNotificationProvider(httpClientFactory.CreateClient("Webhook"), logger);
        });
        services.AddSingleton<INotificationProvider, EmailNotificationProvider>(sp =>
        {
            var logger = sp.GetService<ILogger<EmailNotificationProvider>>();
            return new EmailNotificationProvider(logger);
        });
        services.AddSingleton<INotificationProvider, PushoverNotificationProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetService<ILogger<PushoverNotificationProvider>>();
            return new PushoverNotificationProvider(httpClientFactory.CreateClient("Pushover"), logger);
        });
        services.AddSingleton<INotificationProvider, PushbulletNotificationProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetService<ILogger<PushbulletNotificationProvider>>();
            return new PushbulletNotificationProvider(httpClientFactory.CreateClient("Pushbullet"), logger);
        });
        services.AddSingleton<INotificationProvider, TelegramNotificationProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetService<ILogger<TelegramNotificationProvider>>();
            return new TelegramNotificationProvider(httpClientFactory.CreateClient("Telegram"), logger);
        });

        // Settings (can be overridden via configuration)
        services.Configure<DecisionEngineSettings>(options =>
        {
            // Defaults are set in the class, but can be bound from config here
        });

        return services;
    }
}
