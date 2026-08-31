using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingPlansEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signup_fee_paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_billing_customers_users_master_user_id",
                        column: x => x.master_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    billing_interval_months = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plan_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_offers_plan_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "plan_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_offers_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    price_cents = table.Column<long>(type: "bigint", nullable: false),
                    signup_fee_cents = table.Column<long>(type: "bigint", nullable: false),
                    subaccount_limit = table.Column<int>(type: "integer", nullable: false),
                    monthly_publication_limit = table.Column<int>(type: "integer", nullable: false),
                    marketing_reference_price_cents = table.Column<long>(type: "bigint", nullable: true),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_versions_plan_offers_plan_offer_id",
                        column: x => x.plan_offer_id,
                        principalTable: "plan_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pending_plan_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    past_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    grace_period_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    canceled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscriptions_plan_versions_pending_plan_version_id",
                        column: x => x.pending_plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscriptions_plan_versions_plan_version_id",
                        column: x => x.plan_version_id,
                        principalTable: "plan_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_master_user_id",
                        column: x => x.master_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usage_counters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    window_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publication_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_counters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usage_counters_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_customers_master_user_id",
                table: "billing_customers",
                column: "master_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_categories_key",
                table: "plan_categories",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_offers_category_id",
                table: "plan_offers",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_offers_key",
                table: "plan_offers",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_offers_plan_id_category_id",
                table: "plan_offers",
                columns: new[] { "plan_id", "category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_versions_plan_offer_id_effective_from",
                table: "plan_versions",
                columns: new[] { "plan_offer_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_versions_plan_offer_id_version",
                table: "plan_versions",
                columns: new[] { "plan_offer_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plans_key",
                table: "plans",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_current_period_end",
                table: "subscriptions",
                column: "current_period_end");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_master_user_id_status",
                table: "subscriptions",
                columns: new[] { "master_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_pending_plan_version_id",
                table: "subscriptions",
                column: "pending_plan_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_plan_version_id",
                table: "subscriptions",
                column: "plan_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_counters_subscription_id_window_start",
                table: "usage_counters",
                columns: new[] { "subscription_id", "window_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_customers");

            migrationBuilder.DropTable(
                name: "usage_counters");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "plan_versions");

            migrationBuilder.DropTable(
                name: "plan_offers");

            migrationBuilder.DropTable(
                name: "plan_categories");

            migrationBuilder.DropTable(
                name: "plans");
        }
    }
}
