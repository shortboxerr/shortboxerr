using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Shortboxerr.Infrastructure.Persistence;

/// <summary>
/// Helper for querying the Series_fts FTS5 virtual table (SQLite only).
/// Used by the series list endpoint to apply full-text search when available.
/// </summary>
public static class SeriesFtsHelper
{
    /// <summary>
    /// Returns series IDs that match the FTS5 query. Call only when the database provider is SQLite
    /// and Series_fts exists (after migration AddSeriesFts5).
    /// </summary>
    /// <param name="db">The context (must be using SQLite).</param>
    /// <param name="searchTerm">FTS5 search term (e.g. "batman" or "batman*" for prefix).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of Series.Id values that match; empty if none or on error.</returns>
    public static async Task<IReadOnlyList<int>> GetSeriesIdsFromFtsAsync(
        this ShortboxerrDbContext db,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Array.Empty<int>();

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        // FTS5 MATCH parameter: use a single parameter to avoid injection
        cmd.CommandText = "SELECT rowid FROM Series_fts WHERE Series_fts MATCH $search LIMIT 5000";
        var p = cmd.CreateParameter();
        p.ParameterName = "$search";
        p.Value = searchTerm.Trim();
        cmd.Parameters.Add(p);

        var ids = new List<int>();
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetInt32(0));
        }
        catch (SqliteException)
        {
            // Table might not exist (pre-migration) or invalid FTS syntax; return empty
            return Array.Empty<int>();
        }
        finally
        {
            await conn.CloseAsync();
        }

        return ids;
    }
}
