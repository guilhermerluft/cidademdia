using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterSubaccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_subaccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subaccount_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_subaccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_master_subaccounts_users_master_user_id",
                        column: x => x.master_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_master_subaccounts_users_subaccount_user_id",
                        column: x => x.subaccount_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_subaccount_permissions",
                columns: table => new
                {
                    master_subaccount_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_subaccount_permissions", x => new { x.master_subaccount_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_master_subaccount_permissions_master_subaccounts_master_sub~",
                        column: x => x.master_subaccount_id,
                        principalTable: "master_subaccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_master_subaccount_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_master_subaccount_permissions_permission_id",
                table: "master_subaccount_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_master_subaccounts_master_user_id_status",
                table: "master_subaccounts",
                columns: new[] { "master_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_master_subaccounts_master_user_id_subaccount_user_id",
                table: "master_subaccounts",
                columns: new[] { "master_user_id", "subaccount_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_master_subaccounts_subaccount_user_id",
                table: "master_subaccounts",
                column: "subaccount_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_subaccount_permissions");

            migrationBuilder.DropTable(
                name: "master_subaccounts");
        }
    }
}
