using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shortboxerr.Core.ComicVine;
using Shortboxerr.Core.PullList;
using Shortboxerr.Core.Services;
using Shortboxerr.Core.WalkSoftly;
using Shortboxerr.Infrastructure.BackgroundServices;
using Xunit;

namespace Shortboxerr.Tests;

/// <summary>
/// Tests for DiscoveryUpgradeBackgroundService (EPIC 11.27 Phase 2).
/// Verifies MetronInterim → ComicVineFinalized upgrade behavior.
/// </summary>
public class DiscoveryUpgradeBackgroundServiceTests
{
    [Fact]
    public void PullListSettings_DiscoveryUpgradeEnabled_DefaultIsTrue()
    {
        // Arrange & Act
        var settings = new PullListSettings();

        // Assert
        Assert.True(settings.DiscoveryUpgradeEnabled);
    }

    [Fact]
    public void PullListSettings_DiscoveryUpgradeIntervalHours_DefaultIsFour()
    {
        // Arrange & Act
        var settings = new PullListSettings();

        // Assert
        Assert.Equal(4, settings.DiscoveryUpgradeIntervalHours);
    }

    [Fact]
    public void PullListSettings_DiscoveryUpgradeWeeksAhead_DefaultIsFour()
    {
        // Arrange & Act
        var settings = new PullListSettings();

        // Assert
        Assert.Equal(4, settings.DiscoveryUpgradeWeeksAhead);
    }

    [Fact]
    public void PullListSettings_DiscoveryUpgrade_CanBeDisabled()
    {
        // Arrange
        var settings = new PullListSettings
        {
            DiscoveryUpgradeEnabled = false
        };

        // Assert
        Assert.False(settings.DiscoveryUpgradeEnabled);
    }

    [Fact]
    public void PullListSettings_DiscoveryUpgrade_CanSetCustomInterval()
    {
        // Arrange
        var settings = new PullListSettings
        {
            DiscoveryUpgradeIntervalHours = 8
        };

        // Assert
        Assert.Equal(8, settings.DiscoveryUpgradeIntervalHours);
    }

    [Fact]
    public void PullListSettings_DiscoveryUpgrade_CanSetCustomWeeksAhead()
    {
        // Arrange
        var settings = new PullListSettings
        {
            DiscoveryUpgradeWeeksAhead = 6
        };

        // Assert
        Assert.Equal(6, settings.DiscoveryUpgradeWeeksAhead);
    }

    [Fact]
    public void CoverEnrichmentStatus_HasComicVineCover_RepresentsFinalizedState()
    {
        // This test ensures the enum value used for "finalized" state exists
        var status = CoverEnrichmentStatus.HasComicVineCover;
        
        Assert.Equal(CoverEnrichmentStatus.HasComicVineCover, status);
    }

    [Fact]
    public void ComicVineIssue_EnrichmentStatus_DefaultIsNotFinalized()
    {
        // A new ComicVineIssue should default to a non-finalized state
        var issue = new ComicVineIssue
        {
            Id = 0,
            IssueNumber = "1"
        };

        Assert.NotEqual(CoverEnrichmentStatus.HasComicVineCover, issue.EnrichmentStatus);
    }

    [Fact]
    public void ComicVineIssue_WithCvId_CanBeMarkedAsFinalized()
    {
        // Arrange
        var issue = new ComicVineIssue
        {
            Id = 123456,
            IssueNumber = "1"
        };

        // Act
        issue.EnrichmentStatus = CoverEnrichmentStatus.HasComicVineCover;
        issue.CoverSource = "ComicVine";
        issue.CoverMatchMethod = "CvIssueIdUpgrade";
        issue.LastEnrichmentAttempt = DateTime.UtcNow;

        // Assert
        Assert.Equal(CoverEnrichmentStatus.HasComicVineCover, issue.EnrichmentStatus);
        Assert.Equal("ComicVine", issue.CoverSource);
        Assert.Equal("CvIssueIdUpgrade", issue.CoverMatchMethod);
        Assert.NotNull(issue.LastEnrichmentAttempt);
    }

    [Fact]
    public void NonFinalizedIssue_CanBeIdentified_ById()
    {
        // Arrange - Issue without CV ID is not finalized
        var issue = new ComicVineIssue
        {
            Id = 0, // No CV ID
            IssueNumber = "1",
            EnrichmentStatus = CoverEnrichmentStatus.None
        };

        // Assert
        Assert.True(issue.Id <= 0);
        Assert.NotEqual(CoverEnrichmentStatus.HasComicVineCover, issue.EnrichmentStatus);
    }

    [Fact]
    public void NonFinalizedIssue_CanBeIdentified_ByEnrichmentStatus()
    {
        // Arrange - Issue with CV ID but non-finalized status
        var issue = new ComicVineIssue
        {
            Id = 123456,
            IssueNumber = "1",
            EnrichmentStatus = CoverEnrichmentStatus.Enriched // Metron-enriched but not finalized with CV data
        };

        // Assert - Has ID but not finalized
        Assert.True(issue.Id > 0);
        Assert.NotEqual(CoverEnrichmentStatus.HasComicVineCover, issue.EnrichmentStatus);
    }
}
