using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Service for tracking application setup/onboarding status.
/// </summary>
public class SetupStatusService : ISetupStatusService
{
    private const string OnboardingDismissedKey = "Setup:OnboardingDismissed";
    private const string StepCompletedPrefix = "Setup:StepCompleted:";

    private readonly ShortboxerrDbContext _context;
    private readonly ISettingsService _settingsService;
    private readonly IComicVineClient _comicVineClient;
    private readonly ILogger<SetupStatusService> _logger;

    private static readonly SetupStepDefinition[] StepDefinitions = new[]
    {
        new SetupStepDefinition
        {
            Step = SetupStep.ConfigureComicVine,
            Name = "Configure ComicVine",
            Description = "Add your ComicVine API key to enable metadata lookup, cover images, and release tracking.",
            IsRequired = true,
            Order = 1,
            SettingsPath = "/settings/comicvine"
        },
        new SetupStepDefinition
        {
            Step = SetupStep.ConfigureRootFolder,
            Name = "Set Library Folder",
            Description = "Configure the root folder where your comic library will be organized.",
            IsRequired = true,
            Order = 2,
            SettingsPath = "/settings/media-management"
        },
        new SetupStepDefinition
        {
            Step = SetupStep.AddSeries,
            Name = "Add Series",
            Description = "Add at least one comic series to start tracking and downloading issues.",
            IsRequired = true,
            Order = 3,
            SettingsPath = "/series/add"
        },
        new SetupStepDefinition
        {
            Step = SetupStep.ConfigureDownloadClient,
            Name = "Configure Download Client",
            Description = "Add a download client (SABnzbd, NZBGet, qBittorrent) to enable automated downloads.",
            IsRequired = false,
            Order = 4,
            SettingsPath = "/settings/download-clients"
        },
        new SetupStepDefinition
        {
            Step = SetupStep.ConfigureIndexer,
            Name = "Configure Indexer",
            Description = "Add an indexer (Newznab, DDL site) to search for comic releases.",
            IsRequired = false,
            Order = 5,
            SettingsPath = "/settings/indexers"
        }
    };

    public SetupStatusService(
        ShortboxerrDbContext context,
        ISettingsService settingsService,
        IComicVineClient comicVineClient,
        ILogger<SetupStatusService> logger)
    {
        _context = context;
        _settingsService = settingsService;
        _comicVineClient = comicVineClient;
        _logger = logger;
    }

    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var stepStatuses = new List<SetupStepStatus>();
        var completedCount = 0;
        var requiredCount = 0;
        var requiredCompletedCount = 0;
        SetupStep? currentStep = null;

        foreach (var def in StepDefinitions.OrderBy(d => d.Order))
        {
            var stepComplete = await IsStepCompleteAsync(def.Step, cancellationToken);
            var details = await GetStepDetailsAsync(def.Step, cancellationToken);

            stepStatuses.Add(new SetupStepStatus
            {
                Step = def.Step,
                Name = def.Name,
                Description = def.Description,
                IsComplete = stepComplete,
                IsRequired = def.IsRequired,
                Order = def.Order,
                SettingsPath = def.SettingsPath,
                Details = details
            });

            if (stepComplete)
            {
                completedCount++;
                if (def.IsRequired) requiredCompletedCount++;
            }
            else if (currentStep == null)
            {
                currentStep = def.Step;
            }

            if (def.IsRequired) requiredCount++;
        }

        var isDismissed = await _settingsService.GetAsync<bool>(OnboardingDismissedKey, false, cancellationToken);
        var isComplete = requiredCompletedCount >= requiredCount;
        var percentage = StepDefinitions.Length > 0
            ? (int)Math.Round(100.0 * completedCount / StepDefinitions.Length)
            : 100;

        return new SetupStatus
        {
            IsComplete = isComplete,
            IsDismissed = isDismissed,
            CompletionPercentage = percentage,
            CurrentStep = currentStep,
            Steps = stepStatuses
        };
    }

    public async Task DismissOnboardingAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.SetAsync(OnboardingDismissedKey, true, cancellationToken);
        _logger.LogInformation("User dismissed onboarding wizard");
    }

    public async Task ResetOnboardingAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.SetAsync(OnboardingDismissedKey, false, cancellationToken);
        _logger.LogInformation("Onboarding wizard reset - will show again");
    }

    public async Task CompleteStepAsync(SetupStep step, CancellationToken cancellationToken = default)
    {
        var key = $"{StepCompletedPrefix}{step}";
        await _settingsService.SetAsync(key, true, cancellationToken);
        _logger.LogInformation("Setup step {Step} manually marked as complete", step);
    }

    private async Task<bool> IsStepCompleteAsync(SetupStep step, CancellationToken cancellationToken)
    {
        // First check if manually marked complete
        var manualKey = $"{StepCompletedPrefix}{step}";
        var manuallyCompleted = await _settingsService.GetAsync<bool>(manualKey, false, cancellationToken);
        if (manuallyCompleted) return true;

        // Check actual state
        return step switch
        {
            SetupStep.ConfigureComicVine => await _comicVineClient.IsConfiguredAsync(cancellationToken),
            SetupStep.AddSeries => await _context.Series.AnyAsync(s => s.Monitored, cancellationToken),
            SetupStep.ConfigureRootFolder => await IsRootFolderConfiguredAsync(cancellationToken),
            SetupStep.ConfigureDownloadClient => await IsDownloadClientConfiguredAsync(cancellationToken),
            SetupStep.ConfigureIndexer => await IsIndexerConfiguredAsync(cancellationToken),
            _ => false
        };
    }

    private async Task<string?> GetStepDetailsAsync(SetupStep step, CancellationToken cancellationToken)
    {
        return step switch
        {
            SetupStep.ConfigureComicVine => await _comicVineClient.IsConfiguredAsync(cancellationToken)
                ? "API key configured"
                : "No API key set",
            SetupStep.AddSeries => await GetSeriesDetailsAsync(cancellationToken),
            SetupStep.ConfigureRootFolder => await GetRootFolderDetailsAsync(cancellationToken),
            SetupStep.ConfigureDownloadClient => await GetDownloadClientDetailsAsync(cancellationToken),
            SetupStep.ConfigureIndexer => await GetIndexerDetailsAsync(cancellationToken),
            _ => null
        };
    }

    private async Task<bool> IsRootFolderConfiguredAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetGeneralSettingsAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(settings.ComicLibraryPath) &&
               settings.ComicLibraryPath != "/comics"; // Not just the default
    }

    private async Task<bool> IsDownloadClientConfiguredAsync(CancellationToken cancellationToken)
    {
        return await _context.Providers
            .AnyAsync(p => p.Category == ProviderCategory.DownloadClient && p.IsEnabled, cancellationToken);
    }

    private async Task<bool> IsIndexerConfiguredAsync(CancellationToken cancellationToken)
    {
        return await _context.Providers
            .AnyAsync(p => p.Category == ProviderCategory.Indexer && p.IsEnabled, cancellationToken);
    }

    private async Task<string?> GetSeriesDetailsAsync(CancellationToken cancellationToken)
    {
        var count = await _context.Series.CountAsync(s => s.Monitored, cancellationToken);
        return count switch
        {
            0 => "No series added",
            1 => "1 series monitored",
            _ => $"{count} series monitored"
        };
    }

    private async Task<string?> GetRootFolderDetailsAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetGeneralSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ComicLibraryPath))
            return "No folder configured";
        if (settings.ComicLibraryPath == "/comics")
            return "Using default path";
        return settings.ComicLibraryPath;
    }

    private async Task<string?> GetDownloadClientDetailsAsync(CancellationToken cancellationToken)
    {
        var clients = await _context.Providers
            .Where(p => p.Category == ProviderCategory.DownloadClient && p.IsEnabled)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return clients.Count switch
        {
            0 => "No download clients configured",
            1 => clients[0],
            _ => $"{clients.Count} clients configured"
        };
    }

    private async Task<string?> GetIndexerDetailsAsync(CancellationToken cancellationToken)
    {
        var indexers = await _context.Providers
            .Where(p => p.Category == ProviderCategory.Indexer && p.IsEnabled)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return indexers.Count switch
        {
            0 => "No indexers configured",
            1 => indexers[0],
            _ => $"{indexers.Count} indexers configured"
        };
    }

    private record SetupStepDefinition
    {
        public SetupStep Step { get; init; }
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public bool IsRequired { get; init; }
        public int Order { get; init; }
        public string? SettingsPath { get; init; }
    }
}
