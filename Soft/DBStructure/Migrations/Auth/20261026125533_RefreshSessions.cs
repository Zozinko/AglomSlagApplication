using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBStructure.Migrations.Auth
{
    /// <inheritdoc />
    public partial class RefreshSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshSessions_RefreshSessions_ReplacedById",
                        column: x => x.ReplacedById,
                        principalTable: "RefreshSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_ReplacedById",
                table: "RefreshSessions",
                column: "ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_TokenHash",
                table: "RefreshSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_UserId_FamilyId",
                table: "RefreshSessions",
                columns: new[] { "UserId", "FamilyId" });

            // Run inside the same PostgreSQL transaction as any credential change,
            // including trusted maintenance SQL. Rehash deliberately revokes prior sessions.
            migrationBuilder.Sql("""
                CREATE FUNCTION revoke_user_refresh_sessions() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    UPDATE "RefreshSessions" SET "RevokedAt" = clock_timestamp()
                    WHERE "UserId" = NEW."UserId" AND "RevokedAt" IS NULL;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER users_revoke_refresh_sessions
                AFTER UPDATE OF "PasswordHash", "Disabled" ON "Users"
                FOR EACH ROW WHEN (
                    NEW."PasswordHash" IS DISTINCT FROM OLD."PasswordHash"
                    OR (NEW."Disabled" AND NOT OLD."Disabled")
                ) EXECUTE FUNCTION revoke_user_refresh_sessions();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER users_revoke_refresh_sessions ON "Users";
                DROP FUNCTION revoke_user_refresh_sessions();
                """);
            migrationBuilder.DropTable(
                name: "RefreshSessions");
        }
    }
}
