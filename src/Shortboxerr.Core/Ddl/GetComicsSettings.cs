namespace Shortboxerr.Core.Ddl;

/// <summary>
/// GetComics-specific settings with full Mylar3 parity.
/// Based on Mylar3's GetComics configuration options.
/// </summary>
public class GetComicsSettings
{
    /// <summary>
    /// Base URL for GetComics (default: https://getcomics.org).
    /// </summary>
    public string BaseUrl { get; set; } = "https://getcomics.org";
    
    /// <summary>
    /// Whether GetComics is enabled as a source.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Search query delay in seconds between pages (Mylar3: DDL_QUERY_DELAY).
    /// Prevents rate limiting.
    /// </summary>
    public int QueryDelaySeconds { get; set; } = 5;
    
    /// <summary>
    /// Maximum pages to search through.
    /// </summary>
    public int MaxSearchPages { get; set; } = 5;
    
    /// <summary>
    /// Preferred link type order for downloads.
    /// Based on Mylar3's LINK_PRIORITY order.
    /// Valid values: "mega", "pixeldrain", "mediafire", "main", "mirror"
    /// </summary>
    public List<string> LinkPriority { get; set; } = new() { "mega", "pixeldrain", "mediafire", "main" };
    
    /// <summary>
    /// Quality preference order for HD/SD variants.
    /// Based on Mylar3's quality handling.
    /// Valid values: "sd-digital", "hd-digital", "hd-upscaled", "normal"
    /// </summary>
    public List<string> QualityPreference { get; set; } = new() { "sd-digital", "hd-digital", "normal" };
    
    /// <summary>
    /// Whether to prefer packs when searching.
    /// Based on Mylar3's PACK_PRIORITY setting.
    /// </summary>
    public bool PreferPacks { get; set; } = false;
    
    /// <summary>
    /// Whether to use FlareSolverr for Cloudflare bypass.
    /// </summary>
    public bool UseFlareSolverr { get; set; } = false;
    
    /// <summary>
    /// FlareSolverr URL (if using).
    /// </summary>
    public string? FlareSolverrUrl { get; set; }
    
    /// <summary>
    /// Custom User-Agent string.
    /// If not set, uses Mylar3-compatible Firefox UA.
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// HTTP proxy URL (if using proxy).
    /// </summary>
    public string? HttpProxy { get; set; }
    
    /// <summary>
    /// HTTPS proxy URL (if using proxy).
    /// </summary>
    public string? HttpsProxy { get; set; }
    
    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Whether to verify SSL certificates.
    /// </summary>
    public bool VerifySsl { get; set; } = true;
    
    /// <summary>
    /// DDL download location override.
    /// If not set, uses general download location.
    /// </summary>
    public string? DownloadLocation { get; set; }
    
    /// <summary>
    /// Whether to auto-extract zip files after download.
    /// Based on Mylar3's zip_zip behavior.
    /// </summary>
    public bool AutoExtractZip { get; set; } = true;
    
    /// <summary>
    /// Whether to delete zip file after extraction.
    /// </summary>
    public bool DeleteZipAfterExtract { get; set; } = true;
}

/// <summary>
/// Quality variant types for GetComics downloads.
/// Matches Mylar3's HD/SD distinction.
/// </summary>
public enum GetComicsQualityVariant
{
    /// <summary>
    /// Standard release (no quality specification).
    /// </summary>
    Normal,
    
    /// <summary>
    /// SD (Standard Definition) digital release.
    /// Lower file size, standard resolution.
    /// </summary>
    SdDigital,
    
    /// <summary>
    /// HD (High Definition) digital release.
    /// Higher file size, better resolution.
    /// </summary>
    HdDigital,
    
    /// <summary>
    /// HD Upscaled release.
    /// AI/manual upscaled for higher resolution.
    /// </summary>
    HdUpscaled
}

/// <summary>
/// Link types for GetComics downloads.
/// Based on Mylar3's link_type handling.
/// </summary>
public enum GetComicsLinkType
{
    /// <summary>
    /// Main GetComics server download.
    /// </summary>
    Main,
    
    /// <summary>
    /// Mirror GetComics server download.
    /// </summary>
    Mirror,
    
    /// <summary>
    /// Mega.nz hosted download.
    /// </summary>
    Mega,
    
    /// <summary>
    /// MediaFire hosted download.
    /// </summary>
    MediaFire,
    
    /// <summary>
    /// Pixeldrain hosted download.
    /// </summary>
    Pixeldrain,
    
    /// <summary>
    /// Unknown hosting service.
    /// </summary>
    Unknown
}

/// <summary>
/// Represents a GetComics download link with quality variant info.
/// </summary>
public class GetComicsLink
{
    /// <summary>
    /// Download URL.
    /// </summary>
    public required string Url { get; init; }
    
    /// <summary>
    /// Link type (main, mirror, mega, etc.).
    /// </summary>
    public GetComicsLinkType LinkType { get; init; }
    
    /// <summary>
    /// Quality variant (SD/HD/Upscaled/Normal).
    /// </summary>
    public GetComicsQualityVariant QualityVariant { get; init; } = GetComicsQualityVariant.Normal;
    
    /// <summary>
    /// Series name from the link section.
    /// </summary>
    public string? Series { get; init; }
    
    /// <summary>
    /// Year from the link section.
    /// </summary>
    public string? Year { get; init; }
    
    /// <summary>
    /// Size string from the link section.
    /// </summary>
    public string? Size { get; init; }
    
    /// <summary>
    /// Issues covered (for packs).
    /// </summary>
    public string? Issues { get; init; }
    
    /// <summary>
    /// Whether this is a pack download.
    /// </summary>
    public bool IsPack { get; init; }
    
    /// <summary>
    /// Combined site type key (e.g., "SD-Digital:mega").
    /// Used for Mylar3-style link matching.
    /// </summary>
    public string SiteTypeKey => QualityVariant == GetComicsQualityVariant.Normal 
        ? $"normal:{LinkType.ToString().ToLowerInvariant()}"
        : $"{QualityVariantToString(QualityVariant)}:{LinkType.ToString().ToLowerInvariant()}";
    
    private static string QualityVariantToString(GetComicsQualityVariant variant) => variant switch
    {
        GetComicsQualityVariant.SdDigital => "SD-Digital",
        GetComicsQualityVariant.HdDigital => "HD-Digital",
        GetComicsQualityVariant.HdUpscaled => "HD-Upscaled",
        _ => "normal"
    };
}
