using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shortboxerr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ReleaseTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SourceSite = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ParsedSeriesTitle = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ParsedIssueNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ParsedYear = table.Column<int>(type: "INTEGER", nullable: true),
                    ParsedPublisher = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchFound = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfidenceScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchedSeriesTitle = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MatchedIssueId = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchedIssueNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    WasFirstIssue = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiredManualReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ScoreBreakdownJson = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    ConfidenceReductionsJson = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    UserVerified = table.Column<bool>(type: "INTEGER", nullable: true),
                    CorrectedSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    CorrectedIssueId = table.Column<int>(type: "INTEGER", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchHistories_Issues_CorrectedIssueId",
                        column: x => x.CorrectedIssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchHistories_Issues_MatchedIssueId",
                        column: x => x.MatchedIssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchHistories_Series_CorrectedSeriesId",
                        column: x => x.CorrectedSeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatchHistories_Series_MatchedSeriesId",
                        column: x => x.MatchedSeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_CorrectedIssueId",
                table: "MatchHistories",
                column: "CorrectedIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_CorrectedSeriesId",
                table: "MatchHistories",
                column: "CorrectedSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_MatchedIssueId",
                table: "MatchHistories",
                column: "MatchedIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_MatchedSeriesId",
                table: "MatchHistories",
                column: "MatchedSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_MatchedSeriesId_Timestamp",
                table: "MatchHistories",
                columns: new[] { "MatchedSeriesId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_MatchId",
                table: "MatchHistories",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_Outcome",
                table: "MatchHistories",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_Timestamp",
                table: "MatchHistories",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_MatchHistories_UserVerified",
                table: "MatchHistories",
                column: "UserVerified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchHistories");
        }
    }
}
