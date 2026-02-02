using Shortboxerr.Core.Models;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Parses comic book filenames to extract metadata.
/// </summary>
public interface IFilenameParser
{
    /// <summary>
    /// Parse a filename and extract comic information.
    /// </summary>
    /// <param name="filename">The filename to parse (without path).</param>
    /// <returns>Parsed information and confidence score.</returns>
    (ParsedComicInfo Info, int Confidence, bool IsCollection) Parse(string filename);
}

