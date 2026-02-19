using Shortboxerr.Core.Services;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for application setup and onboarding status.
/// </summary>
public static class SetupEndpoints
{
    public static void MapSetupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/setup")
            .WithTags("Setup & Onboarding")
            .WithOpenApi();

        // GET setup status
        group.MapGet("/status", async (ISetupStatusService setupService, CancellationToken ct) =>
        {
            var status = await setupService.GetStatusAsync(ct);
            return Results.Ok(ToDto(status));
        })
        .WithName("GetSetupStatus")
        .WithDescription("Gets the current setup/onboarding status for all steps.");

        // POST dismiss onboarding
        group.MapPost("/dismiss", async (ISetupStatusService setupService, CancellationToken ct) =>
        {
            await setupService.DismissOnboardingAsync(ct);
            return Results.Ok(new { message = "Onboarding dismissed." });
        })
        .WithName("DismissOnboarding")
        .WithDescription("Dismisses the onboarding wizard (user chose to skip).");

        // POST reset onboarding
        group.MapPost("/reset", async (ISetupStatusService setupService, CancellationToken ct) =>
        {
            await setupService.ResetOnboardingAsync(ct);
            return Results.Ok(new { message = "Onboarding reset - will show again." });
        })
        .WithName("ResetOnboarding")
        .WithDescription("Resets the onboarding dismissal to show the wizard again.");

        // POST complete step manually
        group.MapPost("/steps/{step}/complete", async (SetupStep step, ISetupStatusService setupService, CancellationToken ct) =>
        {
            await setupService.CompleteStepAsync(step, ct);
            var status = await setupService.GetStatusAsync(ct);
            return Results.Ok(ToDto(status));
        })
        .WithName("CompleteSetupStep")
        .WithDescription("Manually marks a setup step as complete.");

        // GET check if should show onboarding (convenience endpoint)
        group.MapGet("/should-onboard", async (ISetupStatusService setupService, CancellationToken ct) =>
        {
            var status = await setupService.GetStatusAsync(ct);
            return Results.Ok(new ShouldOnboardDto
            {
                ShouldShowOnboarding = status.ShouldShowOnboarding,
                IsComplete = status.IsComplete,
                IsDismissed = status.IsDismissed,
                CurrentStep = status.CurrentStep,
                CompletionPercentage = status.CompletionPercentage
            });
        })
        .WithName("ShouldShowOnboarding")
        .WithDescription("Quick check if onboarding wizard should be shown to user.");
    }

    private static SetupStatusDto ToDto(SetupStatus status) => new()
    {
        IsComplete = status.IsComplete,
        IsDismissed = status.IsDismissed,
        ShouldShowOnboarding = status.ShouldShowOnboarding,
        CompletionPercentage = status.CompletionPercentage,
        CurrentStep = status.CurrentStep,
        Steps = status.Steps.Select(s => new SetupStepStatusDto
        {
            Step = s.Step,
            Name = s.Name,
            Description = s.Description,
            IsComplete = s.IsComplete,
            IsRequired = s.IsRequired,
            Order = s.Order,
            SettingsPath = s.SettingsPath,
            Details = s.Details
        }).ToList()
    };
}

// DTOs
public class SetupStatusDto
{
    public bool IsComplete { get; init; }
    public bool IsDismissed { get; init; }
    public bool ShouldShowOnboarding { get; init; }
    public int CompletionPercentage { get; init; }
    public SetupStep? CurrentStep { get; init; }
    public List<SetupStepStatusDto> Steps { get; init; } = new();
}

public class SetupStepStatusDto
{
    public SetupStep Step { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsComplete { get; init; }
    public bool IsRequired { get; init; }
    public int Order { get; init; }
    public string? SettingsPath { get; init; }
    public string? Details { get; init; }
}

public class ShouldOnboardDto
{
    public bool ShouldShowOnboarding { get; init; }
    public bool IsComplete { get; init; }
    public bool IsDismissed { get; init; }
    public SetupStep? CurrentStep { get; init; }
    public int CompletionPercentage { get; init; }
}
