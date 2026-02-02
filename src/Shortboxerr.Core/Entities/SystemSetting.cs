namespace Shortboxerr.Core.Entities;

/// <summary>
/// Stores system-level configuration key-value pairs.
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public string? Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

