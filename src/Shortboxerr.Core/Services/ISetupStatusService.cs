namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for tracking application setup/onboarding status.
/// Used by the UI to show first-time user guidance.
/// </summary>
public interface ISetupStatusService
{
    /// <summary>
    /// Gets the current setup status for all steps.
    /// </summary>
    Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the onboarding as dismissed (user chose to skip).
    /// </summary>
    Task DismissOnboardingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the dismissed state to show onboarding again.
    /// </summary>
    Task ResetOnboardingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a specific setup step as completed manually.
    /// </summary>
    Task CompleteStepAsync(SetupStep step, CancellationToken cancellationToken = default);
}

/// <summary>
/// Overall setup status for the application.
/// </summary>
public class SetupStatus
{
    /// <summary>
    /// Whether all required setup steps are complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Whether the user has dismissed the onboarding wizard.
    /// </summary>
    public bool IsDismissed { get; set; }

    /// <summary>
    /// Whether onboarding should be shown to the user.
    /// True if setup is incomplete AND not dismissed.
    /// </summary>
    public bool ShouldShowOnboarding => !IsComplete && !IsDismissed;

    /// <summary>
    /// Percentage of setup steps completed (0-100).
    /// </summary>
    public int CompletionPercentage { get; set; }

    /// <summary>
    /// The current/next step the user should complete.
    /// Null if all steps are complete.
    /// </summary>
    public SetupStep? CurrentStep { get; set; }

    /// <summary>
    /// Detailed status for each setup step.
    /// </summary>
    public IReadOnlyList<SetupStepStatus> Steps { get; set; } = Array.Empty<SetupStepStatus>();
}

/// <summary>
/// Status of an individual setup step.
/// </summary>
public class SetupStepStatus
{
    /// <summary>
    /// The setup step identifier.
    /// </summary>
    public SetupStep Step { get; set; }

    /// <summary>
    /// Display name for the step.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Description of what this step accomplishes.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Whether this step is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Whether this step is required for basic functionality.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Order in which this step should be completed.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// URL or route for the settings page to complete this step.
    /// </summary>
    public string? SettingsPath { get; set; }

    /// <summary>
    /// Additional details about the current state of this step.
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Setup step identifiers.
/// </summary>
public enum SetupStep
{
    /// <summary>
    /// Configure ComicVine API key for metadata.
    /// </summary>
    ConfigureComicVine = 1,

    /// <summary>
    /// Add at least one series to monitor.
    /// </summary>
    AddSeries = 2,

    /// <summary>
    /// Configure root folder path for comics library.
    /// </summary>
    ConfigureRootFolder = 3,

    /// <summary>
    /// Configure at least one download client.
    /// </summary>
    ConfigureDownloadClient = 4,

    /// <summary>
    /// Configure at least one indexer.
    /// </summary>
    ConfigureIndexer = 5
}
