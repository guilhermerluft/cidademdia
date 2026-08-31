using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOccurrenceTargetAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "occurrence_target_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_subaccount_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_master_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrence_target_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_occurrence_target_assignments_master_subaccounts_master_sub~",
                        column: x => x.master_subaccount_id,
                        principalTable: "master_subaccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_occurrence_target_assignments_occurrence_targets_occurrence~",
                        column: x => x.occurrence_target_id,
                        principalTable: "occurrence_targets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_occurrence_target_assignments_users_assigned_by_master_user~",
                        column: x => x.assigned_by_master_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_target_assignments_assigned_by_master_user_id",
                table: "occurrence_target_assignments",
                column: "assigned_by_master_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_target_assignments_master_subaccount_id",
                table: "occurrence_target_assignments",
                column: "master_subaccount_id");

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_target_assignments_master_subaccount_id_assigned~",
                table: "occurrence_target_assignments",
                columns: new[] { "master_subaccount_id", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_target_assignments_occurrence_target_id",
                table: "occurrence_target_assignments",
                column: "occurrence_target_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occurrence_target_assignments");
        }
    }
}
