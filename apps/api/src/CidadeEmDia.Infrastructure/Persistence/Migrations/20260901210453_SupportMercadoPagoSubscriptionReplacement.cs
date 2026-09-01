using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportMercadoPagoSubscriptionReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_provider_subscriptions_provider_external_reference",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_billing_provider_subscriptions_subscription_id",
                table: "billing_provider_subscriptions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ended_at",
                table: "billing_provider_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_current",
                table: "billing_provider_subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_provider_subscriptions_provider_external_reference_current",
                table: "billing_provider_subscriptions",
                columns: new[] { "provider", "external_reference" },
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "IX_billing_provider_subscriptions_subscription_id_current",
                table: "billing_provider_subscriptions",
                column: "subscription_id",
                unique: true,
                filter: "is_current = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_provider_subscriptions_provider_external_reference_current",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_billing_provider_subscriptions_subscription_id_current",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropColumn(
                name: "ended_at",
                table: "billing_provider_subscriptions");

            migrationBuilder.DropColumn(
                name: "is_current",
                table: "billing_provider_subscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_billing_provider_subscriptions_provider_external_reference",
                table: "billing_provider_subscriptions",
                columns: new[] { "provider", "external_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_provider_subscriptions_subscription_id",
                table: "billing_provider_subscriptions",
                column: "subscription_id",
                unique: true);
        }
    }
}
