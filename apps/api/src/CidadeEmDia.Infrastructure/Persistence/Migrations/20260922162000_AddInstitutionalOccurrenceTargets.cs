using System;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260922162000_AddInstitutionalOccurrenceTargets")]
    public partial class AddInstitutionalOccurrenceTargets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_occurrence_targets_master_user_id_status",
                table: "occurrence_targets");

            migrationBuilder.DropIndex(
                name: "IX_occurrence_targets_occurrence_id_master_user_id",
                table: "occurrence_targets");

            migrationBuilder.AlterColumn<Guid>(
                name: "master_user_id",
                table: "occurrence_targets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "institution_id",
                table: "occurrence_targets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "addressee",
                table: "occurrence_targets",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_occurrence_targets_master_status",
                table: "occurrence_targets",
                columns: new[] { "master_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_occurrence_targets_institution_status",
                table: "occurrence_targets",
                columns: new[] { "institution_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_occurrence_targets_occurrence_master",
                table: "occurrence_targets",
                columns: new[] { "occurrence_id", "master_user_id" },
                unique: true,
                filter: "master_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_occurrence_targets_occurrence_institution",
                table: "occurrence_targets",
                columns: new[] { "occurrence_id", "institution_id" },
                unique: true,
                filter: "institution_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_occurrence_targets_institutions_institution_id",
                table: "occurrence_targets",
                column: "institution_id",
                principalTable: "institutions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_occurrence_targets_institutions_institution_id",
                table: "occurrence_targets");

            migrationBuilder.DropIndex(
                name: "ix_occurrence_targets_institution_status",
                table: "occurrence_targets");

            migrationBuilder.DropIndex(
                name: "ix_occurrence_targets_master_status",
                table: "occurrence_targets");

            migrationBuilder.DropIndex(
                name: "ux_occurrence_targets_occurrence_institution",
                table: "occurrence_targets");

            migrationBuilder.DropIndex(
                name: "ux_occurrence_targets_occurrence_master",
                table: "occurrence_targets");

            migrationBuilder.DropColumn(
                name: "addressee",
                table: "occurrence_targets");

            migrationBuilder.DropColumn(
                name: "institution_id",
                table: "occurrence_targets");

            migrationBuilder.Sql(
                "DELETE FROM occurrence_targets WHERE master_user_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "master_user_id",
                table: "occurrence_targets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_targets_master_user_id_status",
                table: "occurrence_targets",
                columns: new[] { "master_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_occurrence_targets_occurrence_id_master_user_id",
                table: "occurrence_targets",
                columns: new[] { "occurrence_id", "master_user_id" },
                unique: true);
        }
    }
}
