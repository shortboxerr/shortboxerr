namespace Shortboxerr.Core.Ddl;

/// <summary>
/// Parses DDL release titles into structured information.
/// Implements Mylar3-compatible parsing rules.
/// </summary>
public interface IDdlReleaseParser
{
    /// <summary>
    /// Parse a release title into structured information.
    /// </summary>
    DdlParsedInfo Parse(string releaseTitle);
    
    /// <summary>
    /// Extract the file format from a release title or filename.
    /// </summary>
    string? ExtractFormat(string title);
    
    /// <summary>
    /// Normalize a series title for matching.
    /// </summary>
    string NormalizeTitle(string title);
}

