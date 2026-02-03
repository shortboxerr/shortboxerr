using Shortboxerr.Core.Ddl;

namespace Shortboxerr.Api.Dtos;

/// <summary>
/// Request to parse Mylar3 config content.
/// </summary>
public record ParseMylar3ConfigRequest(
    string ConfigContent
);

/// <summary>
/// Request to import from a file path.
/// </summary>
public record ParseMylar3ConfigFileRequest(
    string FilePath
);

/// <summary>
/// Request to execute Mylar3 import.
/// </summary>
public record ExecuteMylar3ImportRequest(
    string ConfigContent,
    bool OverwriteExisting = false,
    bool ImportDisabled = true,
    bool ImportCredentials = true,
    string? NamePrefix = null,
    bool ValidateFirst = true
);

/// <summary>
/// Response for Mylar3 import result.
/// </summary>
public record Mylar3ImportResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<Mylar3DdlProviderDto> DdlProviders { get; init; } = new();
    public Mylar3GeneralSettingsDto? GeneralSettings { get; init; }
    public List<string> UnmappedSections { get; init; } = new();
    public Dictionary<string, List<string>> UnmappedSettings { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string? SourcePath { get; init; }
    public DateTime ImportedAt { get; init; }
    
    public static Mylar3ImportResultDto FromDomain(Mylar3ImportResult result) => new()
    {
        Success = result.Success,
        ErrorMessage = result.ErrorMessage,
        DdlProviders = result.DdlProviders.Select(Mylar3DdlProviderDto.FromDomain).ToList(),
        GeneralSettings = result.GeneralSettings != null ? Mylar3GeneralSettingsDto.FromDomain(result.GeneralSettings) : null,
        UnmappedSections = result.UnmappedSections,
        UnmappedSettings = result.UnmappedSettings,
        Warnings = result.Warnings,
        SourcePath = result.SourcePath,
        ImportedAt = result.ImportedAt
    };
}

/// <summary>
/// DTO for Mylar3 DDL provider.
/// </summary>
public record Mylar3DdlProviderDto
{
    public required string Name { get; init; }
    public required string SiteType { get; init; }
    public string? BaseUrl { get; init; }
    public bool IsEnabled { get; init; }
    public int Priority { get; init; }
    public string? Username { get; init; }
    public bool HasPassword { get; init; }
    public bool HasApiKey { get; init; }
    public DdlProviderSettingsDto? Settings { get; init; }
    public string? OriginalSection { get; init; }
    
    public static Mylar3DdlProviderDto FromDomain(Mylar3DdlProvider provider) => new()
    {
        Name = provider.Name,
        SiteType = provider.SiteType,
        BaseUrl = provider.BaseUrl,
        IsEnabled = provider.IsEnabled,
        Priority = provider.Priority,
        Username = provider.Username,
        HasPassword = !string.IsNullOrEmpty(provider.Password),
        HasApiKey = !string.IsNullOrEmpty(provider.ApiKey),
        Settings = provider.Settings != null ? DdlProviderSettingsDto.FromDomain(provider.Settings) : null,
        OriginalSection = provider.OriginalSection
    };
}

/// <summary>
/// DTO for DDL provider settings.
/// </summary>
public record DdlProviderSettingsDto
{
    public required string SiteType { get; init; }
    public int RateLimitPerMinute { get; init; }
    public int TimeoutSeconds { get; init; }
    public int DownloadTimeoutSeconds { get; init; }
    public int MaxRetries { get; init; }
    public int RetryDelayMs { get; init; }
    public bool UseExponentialBackoff { get; init; }
    public string? UserAgent { get; init; }
    public bool EnableCookies { get; init; }
    public bool RequiresAuth { get; init; }
    public string AuthMethod { get; init; } = "None";
    public bool AutoGrabEnabled { get; init; }
    public int AutoGrabMinScore { get; init; }
    public bool SearchCollections { get; init; }
    public bool SearchSingles { get; init; }
    public List<string> FormatPreference { get; init; } = new();
    public List<string> BannedWords { get; init; } = new();
    
    public static DdlProviderSettingsDto FromDomain(DdlProviderSettings settings) => new()
    {
        SiteType = settings.SiteType,
        RateLimitPerMinute = settings.RateLimitPerMinute,
        TimeoutSeconds = settings.TimeoutSeconds,
        DownloadTimeoutSeconds = settings.DownloadTimeoutSeconds,
        MaxRetries = settings.MaxRetries,
        RetryDelayMs = settings.RetryDelayMs,
        UseExponentialBackoff = settings.UseExponentialBackoff,
        UserAgent = settings.UserAgent,
        EnableCookies = settings.EnableCookies,
        RequiresAuth = settings.RequiresAuth,
        AuthMethod = settings.AuthMethod.ToString(),
        AutoGrabEnabled = settings.AutoGrabEnabled,
        AutoGrabMinScore = settings.AutoGrabMinScore,
        SearchCollections = settings.SearchCollections,
        SearchSingles = settings.SearchSingles,
        FormatPreference = settings.FormatPreference,
        BannedWords = settings.BannedWords
    };
    
    public DdlProviderSettings ToDomain() => new()
    {
        SiteType = SiteType,
        RateLimitPerMinute = RateLimitPerMinute,
        TimeoutSeconds = TimeoutSeconds,
        DownloadTimeoutSeconds = DownloadTimeoutSeconds,
        MaxRetries = MaxRetries,
        RetryDelayMs = RetryDelayMs,
        UseExponentialBackoff = UseExponentialBackoff,
        UserAgent = UserAgent,
        EnableCookies = EnableCookies,
        RequiresAuth = RequiresAuth,
        AuthMethod = Enum.TryParse<DdlAuthMethod>(AuthMethod, out var am) ? am : DdlAuthMethod.None,
        AutoGrabEnabled = AutoGrabEnabled,
        AutoGrabMinScore = AutoGrabMinScore,
        SearchCollections = SearchCollections,
        SearchSingles = SearchSingles,
        FormatPreference = FormatPreference,
        BannedWords = BannedWords
    };
}

/// <summary>
/// DTO for Mylar3 general settings.
/// </summary>
public record Mylar3GeneralSettingsDto
{
    public string? ComicLocation { get; init; }
    public string? QualityProfile { get; init; }
    public bool AutoGrab { get; init; }
    public string? PreferredFormat { get; init; }
    public string? StagingFolder { get; init; }
    public string? DatabasePath { get; init; }
    
    public static Mylar3GeneralSettingsDto FromDomain(Mylar3GeneralSettings settings) => new()
    {
        ComicLocation = settings.ComicLocation,
        QualityProfile = settings.QualityProfile,
        AutoGrab = settings.AutoGrab,
        PreferredFormat = settings.PreferredFormat,
        StagingFolder = settings.StagingFolder,
        DatabasePath = settings.DatabasePath
    };
}

/// <summary>
/// Response for validation report.
/// </summary>
public record Mylar3ValidationReportDto
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> ProvidersToCreate { get; init; } = new();
    public List<string> ExistingProviders { get; init; } = new();
    public List<string> SettingDeviations { get; init; } = new();
    
    public static Mylar3ValidationReportDto FromDomain(Mylar3ValidationReport report) => new()
    {
        IsValid = report.IsValid,
        Errors = report.Errors,
        Warnings = report.Warnings,
        ProvidersToCreate = report.ProvidersToCreate,
        ExistingProviders = report.ExistingProviders,
        SettingDeviations = report.SettingDeviations
    };
}

/// <summary>
/// Response for execution result.
/// </summary>
public record Mylar3ExecutionResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int ProvidersCreated { get; init; }
    public int ProvidersUpdated { get; init; }
    public int ProvidersSkipped { get; init; }
    public List<int> CreatedProviderIds { get; init; } = new();
    public List<ProviderImportDetailDto> Details { get; init; } = new();
    
    public static Mylar3ExecutionResultDto FromDomain(Mylar3ExecutionResult result) => new()
    {
        Success = result.Success,
        ErrorMessage = result.ErrorMessage,
        ProvidersCreated = result.ProvidersCreated,
        ProvidersUpdated = result.ProvidersUpdated,
        ProvidersSkipped = result.ProvidersSkipped,
        CreatedProviderIds = result.CreatedProviderIds,
        Details = result.Details.Select(ProviderImportDetailDto.FromDomain).ToList()
    };
}

/// <summary>
/// Detail for a single provider import.
/// </summary>
public record ProviderImportDetailDto
{
    public required string Name { get; init; }
    public string Action { get; init; } = "Unknown";
    public int? ProviderId { get; init; }
    public string? ErrorMessage { get; init; }
    
    public static ProviderImportDetailDto FromDomain(ProviderImportDetail detail) => new()
    {
        Name = detail.Name,
        Action = detail.Action.ToString(),
        ProviderId = detail.ProviderId,
        ErrorMessage = detail.ErrorMessage
    };
}

/// <summary>
/// Response for DDL provider defaults.
/// </summary>
public record DdlProviderDefaultsDto
{
    public required string SiteType { get; init; }
    public DdlProviderSettingsDto Settings { get; init; } = null!;
    
    public static DdlProviderDefaultsDto Create(string siteType)
    {
        var settings = DdlProviderSettings.CreateMylar3Default(siteType);
        return new DdlProviderDefaultsDto
        {
            SiteType = siteType,
            Settings = DdlProviderSettingsDto.FromDomain(settings)
        };
    }
}



