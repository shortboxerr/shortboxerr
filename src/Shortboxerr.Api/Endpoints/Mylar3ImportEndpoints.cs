using Shortboxerr.Api.Dtos;
using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Api.Endpoints;

/// <summary>
/// API endpoints for Mylar3 configuration import.
/// </summary>
public static class Mylar3ImportEndpoints
{
    public static void MapMylar3ImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mylar3")
            .WithTags("Mylar3 Import")
            .WithOpenApi();

        // Parse config content
        group.MapPost("/parse", (IMylar3ConfigImporter importer, ParseMylar3ConfigRequest request) =>
        {
            var result = importer.ParseConfig(request.ConfigContent);
            return Results.Ok(Mylar3ImportResultDto.FromDomain(result));
        })
        .WithName("ParseMylar3Config")
        .WithDescription("Parse Mylar3 config.ini content and extract DDL provider configurations");

        // Parse config from file path
        group.MapPost("/parse/file", async (IMylar3ConfigImporter importer, ParseMylar3ConfigFileRequest request, CancellationToken ct) =>
        {
            var result = await importer.ParseConfigFileAsync(request.FilePath, ct);
            return Results.Ok(Mylar3ImportResultDto.FromDomain(result));
        })
        .WithName("ParseMylar3ConfigFile")
        .WithDescription("Parse Mylar3 config.ini from a file path");

        // Validate import
        group.MapPost("/validate", async (IMylar3ConfigImporter importer, ParseMylar3ConfigRequest request, CancellationToken ct) =>
        {
            var parseResult = importer.ParseConfig(request.ConfigContent);
            if (!parseResult.Success)
            {
                return Results.BadRequest(new { error = parseResult.ErrorMessage });
            }
            
            var validation = await importer.ValidateImportAsync(parseResult, ct);
            return Results.Ok(Mylar3ValidationReportDto.FromDomain(validation));
        })
        .WithName("ValidateMylar3Import")
        .WithDescription("Validate Mylar3 config import against current system state");

        // Execute import
        group.MapPost("/import", async (IMylar3ConfigImporter importer, ExecuteMylar3ImportRequest request, CancellationToken ct) =>
        {
            var parseResult = importer.ParseConfig(request.ConfigContent);
            if (!parseResult.Success)
            {
                return Results.BadRequest(new { error = parseResult.ErrorMessage });
            }
            
            var options = new Mylar3ImportOptions
            {
                OverwriteExisting = request.OverwriteExisting,
                ImportDisabled = request.ImportDisabled,
                ImportCredentials = request.ImportCredentials,
                NamePrefix = request.NamePrefix,
                ValidateFirst = request.ValidateFirst
            };
            
            var result = await importer.ExecuteImportAsync(parseResult, options, ct);
            return Results.Ok(Mylar3ExecutionResultDto.FromDomain(result));
        })
        .WithName("ExecuteMylar3Import")
        .WithDescription("Execute Mylar3 config import, creating DDL providers in the database");

        // Get DDL provider defaults
        group.MapGet("/defaults", () =>
        {
            var siteTypes = new[] { "GettyComics", "ReadComicOnline", "GetComics", "Generic" };
            var defaults = siteTypes.Select(DdlProviderDefaultsDto.Create).ToList();
            return Results.Ok(defaults);
        })
        .WithName("GetDdlProviderDefaults")
        .WithDescription("Get Mylar3-compatible default settings for all supported DDL site types");

        // Get defaults for specific site type
        group.MapGet("/defaults/{siteType}", (string siteType) =>
        {
            return Results.Ok(DdlProviderDefaultsDto.Create(siteType));
        })
        .WithName("GetDdlProviderDefaultsForSite")
        .WithDescription("Get Mylar3-compatible default settings for a specific DDL site type");
    }
}

