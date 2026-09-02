using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoBillingProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_provider_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_subscription_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    checkout_url = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    provider_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    recurring_amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    initial_amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    signup_fee_included = table.Column<bool>(type: "boolean", nullable: false),
                    first_approved_payment_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recurring_amount_synchronized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_provider_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_provider_subscriptions_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    resource_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    live_mode = table.Column<bool>(type: "boolean", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processing_error = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_payment_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    provider_authorized_payment_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status_detail = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_provider_subscriptions_provider_external_reference",
                table: "billing_provider_subscriptions",
                columns: new[] { "provider", "external_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_provider_subscriptions_provider_provider_subscripti~",
                table: "billing_provider_subscriptions",
                columns: new[] { "provider", "provider_subscription_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_provider_subscriptions_subscription_id",
                table: "billing_provider_subscriptions",
                column: "subscription_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_events_processed_at",
                table: "payment_events",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_events_provider_provider_event_id",
                table: "payment_events",
                columns: new[] { "provider", "provider_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_events_provider_type_resource_id",
                table: "payment_events",
                columns: new[] { "provider", "type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_provider_provider_authorized_payment_id",
                table: "payments",
                columns: new[] { "provider", "provider_authorized_payment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_provider_provider_payment_id",
                table: "payments",
                columns: new[] { "provider", "provider_payment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_subscription_id",
                table: "payments",
                column: "subscription_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_provider_subscriptions");

            migrationBuilder.DropTable(
                name: "payment_events");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
