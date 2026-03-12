using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesFts5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite FTS5 virtual table for full-text search on Series (Title, SortTitle).
            // content='Series' and content_rowid='Id' link to the Series table; triggers keep FTS in sync.
            migrationBuilder.Sql(@"
CREATE VIRTUAL TABLE IF NOT EXISTS Series_fts USING fts5(
    Title,
    SortTitle,
    content='Series',
    content_rowid='Id'
);");

            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS Series_fts_ai AFTER INSERT ON Series BEGIN
    INSERT INTO Series_fts(rowid, Title, SortTitle) VALUES (new.Id, new.Title, COALESCE(new.SortTitle, ''));
END;");
            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS Series_fts_ad AFTER DELETE ON Series BEGIN
    INSERT INTO Series_fts(Series_fts, rowid) VALUES ('delete', old.Id);
END;");
            migrationBuilder.Sql(@"
CREATE TRIGGER IF NOT EXISTS Series_fts_au AFTER UPDATE ON Series BEGIN
    INSERT INTO Series_fts(Series_fts, rowid) VALUES ('delete', old.Id);
    INSERT INTO Series_fts(rowid, Title, SortTitle) VALUES (new.Id, new.Title, COALESCE(new.SortTitle, ''));
END;");

            // Populate FTS from existing Series rows
            migrationBuilder.Sql(@"
INSERT INTO Series_fts(rowid, Title, SortTitle) SELECT Id, Title, COALESCE(SortTitle, '') FROM Series;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Series_fts_au;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Series_fts_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Series_fts_ai;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS Series_fts;");
        }
    }
}
