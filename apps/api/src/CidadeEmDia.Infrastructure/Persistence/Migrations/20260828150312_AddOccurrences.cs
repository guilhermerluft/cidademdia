using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "occurrence_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrence_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    public_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_protocol_number = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    external_protocol_agency = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    address_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    location = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "FK_occurrences_occurrence_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "occurrence_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_occurrences_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "occurrence_complements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    occurrence_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrence_complements", x => x.id);
                    table.ForeignKey(
                        name: "FK_occurrence_complements_occurrences_occurrence_id",
                        column: x => x.occurrence_id,
                        principalTable: "occurrences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_occurrence_complements_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "occurrence_service_forecasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    estimated_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    defined_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    defined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    occurrence_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrence_service_forecasts", x => x.id);
                    table.ForeignKey(
                        name: "FK_occurrence_service_forecasts_occurrences_occurrence_id",
                        column: x => x.occurrence_id,
                        principalTable: "occurrences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_occurrence_service_forecasts_users_defined_by_user_id",
                        column: x => x.defined_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "occurrence_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    occurrence_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrence_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_occurrence_status_history_occurrences_occurrence_id",
                        column: x => x.occurrence_id,
                        principalTable: "occurrences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_occurrence_status_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_categories_slug",
                table: "occurrence_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_categories_status_display_order",
                table: "occurrence_categories",
                columns: new[] { "status", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_complements_author_user_id",
                table: "occurrence_complements",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_complements_occurrence_id_created_at",
                table: "occurrence_complements",
                columns: new[] { "occurrence_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_service_forecasts_defined_by_user_id",
                table: "occurrence_service_forecasts",
                column: "defined_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_service_forecasts_occurrence_id_defined_at",
                table: "occurrence_service_forecasts",
                columns: new[] { "occurrence_id", "defined_at" });

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_status_history_changed_by_user_id",
                table: "occurrence_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_status_history_occurrence_id_created_at",
                table: "occurrence_status_history",
                columns: new[] { "occurrence_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_occurrences_author_user_id",
                table: "occurrences",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrences_category_id",
                table: "occurrences",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrences_city_id",
                table: "occurrences",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrences_created_at",
                table: "occurrences",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_occurrences_location_gist",
                table: "occurrences",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_occurrences_public_code",
                table: "occurrences",
                column: "public_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_occurrences_status",
                table: "occurrences",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occurrence_complements");

            migrationBuilder.DropTable(
                name: "occurrence_service_forecasts");

            migrationBuilder.DropTable(
                name: "occurrence_status_history");

            migrationBuilder.DropTable(
                name: "occurrences");

            migrationBuilder.DropTable(
                name: "occurrence_categories");
        }
    }
}
