using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPostsPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    publisher_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    link_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.id);
                    table.ForeignKey(
                        name: "FK_posts_users_master_user_id",
                        column: x => x.master_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_posts_users_publisher_user_id",
                        column: x => x.publisher_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "post_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploader_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    expected_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    actual_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_media", x => x.id);
                    table.ForeignKey(
                        name: "FK_post_media_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_media_users_uploader_user_id",
                        column: x => x.uploader_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "post_placements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placement_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_placements", x => x.id);
                    table.ForeignKey(
                        name: "FK_post_placements_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_post_media_post_sort_order",
                table: "post_media",
                columns: new[] { "post_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_post_media_uploader_user_id",
                table: "post_media",
                column: "uploader_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_post_media_object_key",
                table: "post_media",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_post_placements_key_priority_order",
                table: "post_placements",
                columns: new[] { "placement_key", "priority", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ux_post_placements_post_key",
                table: "post_placements",
                columns: new[] { "post_id", "placement_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_posts_master_status_created",
                table: "posts",
                columns: new[] { "master_user_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_posts_publisher_user_id",
                table: "posts",
                column: "publisher_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_posts_status_published_at",
                table: "posts",
                columns: new[] { "status", "published_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_media");

            migrationBuilder.DropTable(
                name: "post_placements");

            migrationBuilder.DropTable(
                name: "posts");
        }
    }
}
