using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoScheduledReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_mp_binding_externalref_current",
                table: "billing_provider_subscriptions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_for",
                table: "billing_provider_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_plan_version_id",
                table: "billing_provider_subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_binding_target_plan",
                table: "billing_provider_subscriptions",
                column: "target_plan_version_id");

            migrationBuilder.CreateIndex(
                name: "UX_mp_binding_externalref",
                table: "billing_provider_subscriptions",
                columns: new[] { "provider", "external_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_mp_binding_scheduled",
                table: "billing_provider_subscriptions",
                columns: new[] { "subscription_id", "is_current" },
                unique: true,
                filter: "is_current = false AND ended_at IS NULL AND target_plan_version_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_mp_binding_target_plan",
                table: "billing_provider_subscriptions",
                column: "target_plan_version_id",
                principalTable: "plan_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mp_binding_target_plan",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_mp_binding_target_plan",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_mp_binding_externalref",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_mp_binding_scheduled",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropColumn(
                name: "scheduled_for",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropColumn(
                name: "target_plan_version_id",
                table: "billing_provider_subscriptions");

            migrationBuilder.CreateIndex(
                name: "UX_mp_binding_externalref_current",
                table: "billing_provider_subscriptions",
                columns: new[] { "provider", "external_reference" },
                unique: true,
                filter: "is_current = true");
        }
    }
}
