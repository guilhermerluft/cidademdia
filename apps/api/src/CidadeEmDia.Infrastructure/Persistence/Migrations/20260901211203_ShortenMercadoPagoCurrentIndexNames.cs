using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShortenMercadoPagoCurrentIndexNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_billing_provider_subscriptions_subscription_id_current",
                table: "billing_provider_subscriptions",
                newName: "UX_mp_binding_subscription_current");

            migrationBuilder.RenameIndex(
                name: "IX_billing_provider_subscriptions_provider_external_reference_current",
                table: "billing_provider_subscriptions",
                newName: "UX_mp_binding_externalref_current");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "UX_mp_binding_subscription_current",
                table: "billing_provider_subscriptions",
                newName: "IX_billing_provider_subscriptions_subscription_id_current");

            migrationBuilder.RenameIndex(
                name: "UX_mp_binding_externalref_current",
                table: "billing_provider_subscriptions",
                newName: "IX_billing_provider_subscriptions_provider_external_reference_current");
        }
    }
}
