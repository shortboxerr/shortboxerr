using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for DDL import operations.
/// Handles post-download processing and import handoff.
/// </summary>
public static class DdlImportEndpoints
{
    public static void MapDdlImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ddl/import")
            .WithTags("DDL Import")
            .WithOpenApi();

        // Process a completed download
        group.MapPost("/process", async (IDdlImportService importService, ProcessDownloadRequest request) =>
        {
            var candidate = request.Candidate.ToDomain();
            var options = request.Options?.ToDomain();
            
            // Create a download result representing the completed download
            var downloadResult = DdlDownloadResult.Succeeded(
                downloadId: Guid.NewGuid().ToString(),
                filePath: request.FilePath,
                fileName: Path.GetFileName(request.FilePath),
                fileSize: File.Exists(request.FilePath) ? new FileInfo(request.FilePath).Length : 0,
                duration: TimeSpan.FromMinutes(1),
                sourceUrl: candidate.SourceUrl
            );
            
            var result = await importService.ProcessDownloadAsync(downloadResult, candidate, options);
            return Results.Ok(DdlImportResultDto.FromDomain(result));
        })
        .WithName("ProcessDdlDownload")
        .WithDescription("Process a completed DDL download and prepare for import");

        // Verify a downloaded file
        group.MapPost("/verify", async (IDdlImportService importService, VerifyFileRequest request) =>
        {
            var candidate = request.Candidate?.ToDomain() ?? new DdlCandidate
            {
                Id = Guid.NewGuid().ToString(),
                ReleaseTitle = Path.GetFileName(request.FilePath),
                SourceSite = "Manual",
                ParsedInfo = new DdlParsedInfo()
            };
            
            var result = await importService.VerifyFileAsync(request.FilePath, candidate);
            return Results.Ok(DdlVerificationResultDto.FromDomain(result));
        })
        .WithName("VerifyDdlFile")
        .WithDescription("Verify a downloaded file is valid for import");

        // Move file to staging
        group.MapPost("/stage", async (IDdlImportService importService, MoveToStagingRequest request) =>
        {
            var candidate = request.Candidate.ToDomain();
            var result = await importService.MoveToStagingAsync(request.SourcePath, candidate);
            return Results.Ok(DdlStagingResultDto.FromDomain(result));
        })
        .WithName("MoveDdlToStaging")
        .WithDescription("Move a verified file to the staging folder");

        // Auto-match a candidate
        group.MapPost("/match", async (IDdlImportService importService, AutoMatchRequest request) =>
        {
            var candidate = request.Candidate.ToDomain();
            var result = await importService.AutoMatchAsync(candidate);
            return Results.Ok(DdlMatchResultDto.FromDomain(result));
        })
        .WithName("AutoMatchDdlCandidate")
        .WithDescription("Auto-match a candidate to existing series/issue in the database");

        // Execute import for a staged file
        group.MapPost("/execute", async (IDdlImportService importService, ExecuteImportRequest request) =>
        {
            var candidate = request.Candidate.ToDomain();
            
            DdlMatchResult? match = null;
            if (request.SeriesId.HasValue)
            {
                match = new DdlMatchResult
                {
                    MatchFound = true,
                    Confidence = 100,
                    Explanation = "Manually specified match"
                };
                // Note: The actual series/issue will be looked up in the service
            }
            
            var result = await importService.ExecuteImportAsync(request.StagedFilePath, candidate, match);
            return Results.Ok(DdlImportResultDto.FromDomain(result));
        })
        .WithName("ExecuteDdlImport")
        .WithDescription("Execute import for a staged file");

        // Get pending imports
        group.MapGet("/pending", async (IDdlImportService importService) =>
        {
            var pending = await importService.GetPendingImportsAsync();
            return Results.Ok(pending.Select(DdlPendingImportDto.FromDomain));
        })
        .WithName("GetPendingDdlImports")
        .WithDescription("Get pending imports awaiting manual review");

        // Approve a pending import
        group.MapPost("/pending/{id}/approve", async (IDdlImportService importService, string id, ApprovePendingImportRequest request) =>
        {
            if (request.PendingImportId != id)
            {
                return Results.BadRequest("Pending import ID in URL does not match request body");
            }
            
            var result = await importService.ApprovePendingImportAsync(request.PendingImportId, request.SeriesId, request.IssueId);
            return Results.Ok(DdlImportResultDto.FromDomain(result));
        })
        .WithName("ApprovePendingDdlImport")
        .WithDescription("Approve a pending import for processing");

        // Reject a pending import
        group.MapPost("/pending/{id}/reject", async (IDdlImportService importService, string id, RejectPendingImportRequest request) =>
        {
            if (request.PendingImportId != id)
            {
                return Results.BadRequest("Pending import ID in URL does not match request body");
            }
            
            var success = await importService.RejectPendingImportAsync(request.PendingImportId, request.Reason, request.DeleteFile);
            return success 
                ? Results.Ok(new { message = "Pending import rejected successfully" })
                : Results.NotFound(new { error = "Pending import not found" });
        })
        .WithName("RejectPendingDdlImport")
        .WithDescription("Reject a pending import and optionally delete the file");
    }
}

