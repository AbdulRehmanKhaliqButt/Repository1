using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentAgent.Persistence.Migrations;

[DbContext(typeof(InvestigationDbContext))]
[Migration("202609210001_InitialInvestigationPersistence")]
public partial class InitialInvestigationPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "investigations",
            columns: table => new
            {
                Id = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                GeneratedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                Title = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false),
                Description = table.Column<string>(
                    type: "text",
                    nullable: false),
                ServiceName = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true),
                StartedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                Summary = table.Column<string>(
                    type: "text",
                    nullable: false),
                ReasoningMode = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true),
                ReasoningProvider = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true),
                ReasoningModel = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: true),
                ReasoningInputTokens = table.Column<int>(
                    type: "integer",
                    nullable: false),
                ReasoningOutputTokens = table.Column<int>(
                    type: "integer",
                    nullable: false),
                ReasoningLatencyMs = table.Column<long>(
                    type: "bigint",
                    nullable: false),
                ReasoningEstimatedCostUsd = table.Column<decimal>(
                    type: "numeric(18,8)",
                    precision: 18,
                    scale: 8,
                    nullable: false),
                ReasoningFallbackReason = table.Column<string>(
                    type: "text",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_investigations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "investigation_actions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvestigationId = table.Column<string>(
                    type: "character varying(64)",
                    nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                Text = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_investigation_actions", x => x.Id);
                table.ForeignKey(
                    name: "FK_investigation_actions_investigations_InvestigationId",
                    column: x => x.InvestigationId,
                    principalTable: "investigations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "investigation_evidence",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvestigationId = table.Column<string>(
                    type: "character varying(64)",
                    nullable: false),
                EvidenceId = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: false),
                Type = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false),
                Source = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                TimestampUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                Service = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: false),
                Summary = table.Column<string>(type: "text", nullable: false),
                Details = table.Column<string>(type: "text", nullable: false),
                AttributesJson = table.Column<string>(
                    type: "jsonb",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_investigation_evidence", x => x.Id);
                table.ForeignKey(
                    name: "FK_investigation_evidence_investigations_InvestigationId",
                    column: x => x.InvestigationId,
                    principalTable: "investigations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "investigation_hypotheses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvestigationId = table.Column<string>(
                    type: "character varying(64)",
                    nullable: false),
                Rank = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false),
                Explanation = table.Column<string>(type: "text", nullable: false),
                Confidence = table.Column<double>(
                    type: "double precision",
                    nullable: false),
                EvidenceIdsJson = table.Column<string>(
                    type: "jsonb",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_investigation_hypotheses", x => x.Id);
                table.ForeignKey(
                    name: "FK_investigation_hypotheses_investigations_InvestigationId",
                    column: x => x.InvestigationId,
                    principalTable: "investigations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "source_executions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvestigationId = table.Column<string>(
                    type: "character varying(64)",
                    nullable: false),
                Source = table.Column<string>(
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false),
                Status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                DurationMs = table.Column<long>(
                    type: "bigint",
                    nullable: false),
                EvidenceCount = table.Column<int>(
                    type: "integer",
                    nullable: false),
                ErrorType = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_source_executions", x => x.Id);
                table.ForeignKey(
                    name: "FK_source_executions_investigations_InvestigationId",
                    column: x => x.InvestigationId,
                    principalTable: "investigations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_investigations_GeneratedAtUtc",
            table: "investigations",
            column: "GeneratedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_investigations_ServiceName_GeneratedAtUtc",
            table: "investigations",
            columns: new[] { "ServiceName", "GeneratedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_investigation_actions_InvestigationId_SortOrder",
            table: "investigation_actions",
            columns: new[] { "InvestigationId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_investigation_evidence_InvestigationId_EvidenceId",
            table: "investigation_evidence",
            columns: new[] { "InvestigationId", "EvidenceId" });

        migrationBuilder.CreateIndex(
            name: "IX_investigation_hypotheses_InvestigationId_Rank",
            table: "investigation_hypotheses",
            columns: new[] { "InvestigationId", "Rank" });

        migrationBuilder.CreateIndex(
            name: "IX_source_executions_InvestigationId_Source",
            table: "source_executions",
            columns: new[] { "InvestigationId", "Source" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "investigation_actions");
        migrationBuilder.DropTable(name: "investigation_evidence");
        migrationBuilder.DropTable(name: "investigation_hypotheses");
        migrationBuilder.DropTable(name: "source_executions");
        migrationBuilder.DropTable(name: "investigations");
    }
}
