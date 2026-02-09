namespace Shortboxerr.Core.Nzb;

/// <summary>
/// Service for filtering NZB candidates based on configured rules.
/// </summary>
public interface INzbFilterService
{
    /// <summary>
    /// Filters a single NZB candidate against the configured settings.
    /// </summary>
    /// <param name="candidate">The candidate to filter</param>
    /// <param name="settings">Filter settings (null = use defaults)</param>
    /// <returns>Filter result indicating acceptance or rejection</returns>
    NzbFilterResult Filter(NzbCandidate candidate, NzbFilterSettings? settings = null);
    
    /// <summary>
    /// Filters a collection of NZB candidates.
    /// </summary>
    /// <param name="candidates">The candidates to filter</param>
    /// <param name="settings">Filter settings (null = use defaults)</param>
    /// <returns>Candidates that passed filtering, with updated quality scores</returns>
    IEnumerable<NzbCandidate> FilterMany(IEnumerable<NzbCandidate> candidates, NzbFilterSettings? settings = null);
    
    /// <summary>
    /// Applies filters and sorts candidates by quality score.
    /// </summary>
    /// <param name="candidates">The candidates to process</param>
    /// <param name="settings">Filter settings (null = use defaults)</param>
    /// <returns>Filtered and sorted candidates (best first)</returns>
    IReadOnlyList<NzbCandidate> FilterAndSort(IEnumerable<NzbCandidate> candidates, NzbFilterSettings? settings = null);
    
    /// <summary>
    /// Gets the default filter settings.
    /// </summary>
    NzbFilterSettings GetDefaultSettings();
}
