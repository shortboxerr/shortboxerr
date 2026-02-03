using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Core.Models;
using Shortboxerr.Core.Providers;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.ComicVine;
using Shortboxerr.Infrastructure.Ddl;
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
        services.AddScoped<ISettingsService, SettingsService>();

        // DDL services
        services.AddSingleton<IDdlReleaseParser, DdlReleaseParser>();
        services.AddSingleton<IDdlFilter, DdlFilter>();
        services.AddSingleton<IDdlSiteAdapterFactory, DdlSiteAdapterFactory>();
        services.AddSingleton<IDdlSearchService, DdlSearchService>();
        services.AddSingleton<IDdlDownloadService, DdlDownloadService>();
        services.AddScoped<IDdlImportService, DdlImportService>();
        services.AddScoped<IMylar3ConfigImporter, Mylar3ConfigImporter>();

        // Provider system
        services.AddSingleton<IProviderFactory, ProviderFactory>();
        services.AddScoped<IProviderManager, ProviderManager>();

        // ComicVine client and services
        services.AddMemoryCache();
        services.AddHttpClient<IComicVineClient, ComicVineClient>();
        services.AddScoped<ISeriesMetadataService, SeriesMetadataService>();
        services.AddScoped<IIssueMetadataService, IssueMetadataService>();
        services.AddScoped<IEditionMetadataService, EditionMetadataService>();
        services.AddScoped<IAutoMatchService, AutoMatchService>();
        services.AddScoped<IMetadataRefreshService, MetadataRefreshService>();

        // Background services
        services.AddHostedService<BackgroundServices.MetadataRefreshBackgroundService>();

        // Cover service
        services.AddHttpClient("CoverDownload");
        services.AddScoped<ICoverService, CoverService>();

        // Settings (can be overridden via configuration)
        services.Configure<DecisionEngineSettings>(options =>
        {
            // Defaults are set in the class, but can be bound from config here
        });

        return services;
    }
}
