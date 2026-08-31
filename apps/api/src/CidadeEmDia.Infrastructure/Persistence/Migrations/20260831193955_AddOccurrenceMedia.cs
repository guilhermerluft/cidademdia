using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOccurrenceMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "occurrence_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploader_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    expected_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    actual_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrence_media", x => x.id);
                    table.ForeignKey(
                        name: "FK_occurrence_media_occurrences_occurrence_id",
                        column: x => x.occurrence_id,
                        principalTable: "occurrences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_occurrence_media_users_uploader_user_id",
                        column: x => x.uploader_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_occurrence_media_occurrence_id",
                table: "occurrence_media",
                column: "occurrence_id");

            migrationBuilder.CreateIndex(
                name: "ix_occurrence_media_uploader_status_created",
                table: "occurrence_media",
                columns: new[] { "uploader_user_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_occurrence_media_object_key",
                table: "occurrence_media",
                column: "object_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occurrence_media");
        }
    }
}
