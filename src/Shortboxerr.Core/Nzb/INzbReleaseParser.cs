namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Interface for parsing NZB/Usenet release names.
/// </summary>
public interface INzbReleaseParser
{
    /// <summary>
    /// Parses an NZB release title and extracts structured metadata.
    /// </summary>
    /// <param name="releaseTitle">The release title from the NZB indexer</param>
    /// <returns>Parsed information with confidence score</returns>
    NzbParsedInfo Parse(string releaseTitle);
    
    /// <summary>
    /// Parses a NewznabRelease and creates an NzbCandidate.
    /// </summary>
    /// <param name="release">The Newznab release to parse</param>
    /// <param name="indexerPriority">Priority of the source indexer</param>
    /// <returns>An NzbCandidate with parsed information</returns>
    NzbCandidate ParseRelease(NewznabRelease release, int indexerPriority = 50);
    
    /// <summary>
    /// Calculates a quality score for the release based on parsed info.
    /// </summary>
    /// <param name="info">The parsed release information</param>
    /// <returns>Quality score (higher is better)</returns>
    int CalculateQualityScore(NzbParsedInfo info);
}
