using Shortboxerr.Core.Entities;
using Shortboxerr.Core.Providers;

namespace Shortboxerr.Api.Dtos;

/// <summary>
/// DTO for provider definition.
/// </summary>
public record ProviderDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Implementation { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int Priority { get; init; }
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public string? Username { get; init; }
    public bool HasPassword { get; init; }
    public string? Settings { get; init; }
    public string? Tags { get; init; }
    public string LastHealthStatus { get; init; } = string.Empty;
    public DateTime? LastHealthCheck { get; init; }
    public string? LastError { get; init; }
    public int FailureCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static ProviderDto FromEntity(ProviderDefinition entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Implementation = entity.Implementation,
        Category = entity.Category.ToString(),
        Type = entity.Type.ToString(),
        IsEnabled = entity.IsEnabled,
        Priority = entity.Priority,
        BaseUrl = entity.BaseUrl,
        ApiKey = MaskApiKey(entity.ApiKey),
        Username = entity.Username,
        HasPassword = !string.IsNullOrEmpty(entity.Password),
        Settings = entity.Settings,
        Tags = entity.Tags,
        LastHealthStatus = entity.LastHealthStatus.ToString(),
        LastHealthCheck = entity.LastHealthCheck,
        LastError = entity.LastError,
        FailureCount = entity.FailureCount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 8)
        {
            return apiKey != null ? "****" : null;
        }
        return apiKey[..4] + "****" + apiKey[^4..];
    }
}

/// <summary>
/// Request to create a provider.
/// </summary>
public record CreateProviderRequest
{
    public required string Name { get; init; }
    public required string Implementation { get; init; }
    public bool IsEnabled { get; init; } = true;
    public int Priority { get; init; }
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Settings { get; init; }
    public string? Tags { get; init; }

    public ProviderDefinition ToEntity(ProviderCategory category, ProviderType type) => new()
    {
        Name = Name,
        Implementation = Implementation,
        Category = category,
        Type = type,
        IsEnabled = IsEnabled,
        Priority = Priority,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Username = Username,
        Password = Password,
        Settings = Settings,
        Tags = Tags
    };
}

/// <summary>
/// Request to update a provider.
/// </summary>
public record UpdateProviderRequest
{
    public string? Name { get; init; }
    public bool? IsEnabled { get; init; }
    public int? Priority { get; init; }
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Settings { get; init; }
    public string? Tags { get; init; }
}

/// <summary>
/// Request to reorder providers.
/// </summary>
public record ReorderProvidersRequest
{
    public required List<int> OrderedIds { get; init; }
}

/// <summary>
/// DTO for provider test result.
/// </summary>
public record ProviderTestResultDto
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public int? SampleResultCount { get; init; }
    public long? LatencyMs { get; init; }
    public List<string> Errors { get; init; } = new();

    public static ProviderTestResultDto FromResult(ProviderTestResult result) => new()
    {
        Success = result.Success,
        Message = result.Message,
        SampleResultCount = result.SampleResultCount,
        LatencyMs = result.LatencyMs,
        Errors = result.Errors
    };
}

/// <summary>
/// DTO for provider implementation info.
/// </summary>
public record ProviderImplementationDto
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool RequiresBaseUrl { get; init; }
    public bool RequiresApiKey { get; init; }
    public bool RequiresCredentials { get; init; }
    public string? SettingsSchema { get; init; }
}

