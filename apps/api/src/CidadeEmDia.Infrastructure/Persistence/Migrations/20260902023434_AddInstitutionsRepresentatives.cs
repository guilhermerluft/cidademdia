using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CidadeEmDia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionsRepresentatives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "institutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    scope_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    official_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    official_domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    logo_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "institution_jurisdictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jurisdiction_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    custom_area_label = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_jurisdictions", x => x.id);
                    table.ForeignKey(
                        name: "FK_institution_jurisdictions_institutions_institution_id",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "representatives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    public_role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    official_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    photo_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mandate_start = table.Column<DateOnly>(type: "date", nullable: true),
                    mandate_end = table.Column<DateOnly>(type: "date", nullable: true),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    profile_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_representatives", x => x.id);
                    table.ForeignKey(
                        name: "FK_representatives_institutions_institution_id",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_representatives_users_account_id",
                        column: x => x.account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institution_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    representative_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expected_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_invites", x => x.id);
                    table.ForeignKey(
                        name: "FK_institution_invites_institutions_institution_id",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_institution_invites_representatives_representative_id",
                        column: x => x.representative_id,
                        principalTable: "representatives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_institution_invites_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institution_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    representative_id = table.Column<Guid>(type: "uuid", nullable: true),
                    membership_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_institution_memberships_institutions_institution_id",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_institution_memberships_representatives_representative_id",
                        column: x => x.representative_id,
                        principalTable: "representatives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_institution_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_institution_invites_created_by_user_id",
                table: "institution_invites",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_institution_invites_institution_status_expiry",
                table: "institution_invites",
                columns: new[] { "institution_id", "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_institution_invites_representative_status",
                table: "institution_invites",
                columns: new[] { "representative_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_institution_invites_token_hash",
                table: "institution_invites",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_institution_jurisdictions_institution_type",
                table: "institution_jurisdictions",
                columns: new[] { "institution_id", "jurisdiction_type" });

            migrationBuilder.CreateIndex(
                name: "ix_institution_jurisdictions_state_city",
                table: "institution_jurisdictions",
                columns: new[] { "state_code", "city_id" });

            migrationBuilder.CreateIndex(
                name: "ix_institution_membership_user_status",
                table: "institution_memberships",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_institution_memberships_representative_id",
                table: "institution_memberships",
                column: "representative_id");

            migrationBuilder.CreateIndex(
                name: "ux_institution_membership_user_role",
                table: "institution_memberships",
                columns: new[] { "institution_id", "user_id", "membership_role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_institutions_status_type_state",
                table: "institutions",
                columns: new[] { "status", "type", "state_code" });

            migrationBuilder.CreateIndex(
                name: "ux_institutions_cnpj",
                table: "institutions",
                column: "cnpj",
                unique: true,
                filter: "cnpj IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_institutions_slug",
                table: "institutions",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_representatives_institution_status_order",
                table: "representatives",
                columns: new[] { "institution_id", "profile_status", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ux_representatives_account",
                table: "representatives",
                column: "account_id",
                unique: true,
                filter: "account_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_representatives_slug",
                table: "representatives",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institution_invites");

            migrationBuilder.DropTable(
                name: "institution_jurisdictions");

            migrationBuilder.DropTable(
                name: "institution_memberships");

            migrationBuilder.DropTable(
                name: "representatives");

            migrationBuilder.DropTable(
                name: "institutions");
        }
    }
}
