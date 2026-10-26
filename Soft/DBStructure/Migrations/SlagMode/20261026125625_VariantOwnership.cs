using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBStructure.Migrations.SlagMode
{
    /// <inheritdoc />
    public partial class VariantOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "CalcVariants",
                type: "integer",
                nullable: true);

            // Pre-authentication rows had no trustworthy owner. The explicit demo identity
            // owns the legacy dataset; never leave unassigned UserId=0 rows after upgrade.
            migrationBuilder.Sql("UPDATE \"CalcVariants\" SET \"UserId\" = -1;");
            migrationBuilder.Sql("UPDATE \"ComponentsGuide\" SET \"UserId\" = -1;");
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "CalcVariants",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "CalcVariants",
                columns: new[] { "VariantId", "BfId", "CalcDate", "CiCContent", "CiCrContent", "CiMnContent", "CiSContent", "CiSiContent", "CiTemperature", "CiTiContent", "CokeAshAl2O3Content", "CokeAshAmount", "CokeAshCaOContent", "CokeAshMgOContent", "CokeAshSiO2Content", "CokeConsumption", "CokeSContent", "IsImport", "ParentId", "SaveTime", "SlagAl2O3Content", "SlagCaOContent", "SlagMgOContent", "SlagSiO2Content", "SlagTiO2Content", "UserId", "VariantName" },
                values: new object[] { -2, -1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4.702f, 0f, 0.2f, 0.016f, 0.512f, 1450f, 0f, 24.6f, 12.7f, 7.8f, 2f, 48.1f, 419.8f, 0.428f, false, null, null, 24.6f, 40.9f, 2f, 36.56f, 0.01f, -2, "Admin demonstration variant" });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentsGuide_UserId",
                table: "ComponentsGuide",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalcVariants_UserId",
                table: "CalcVariants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ComponentsGuide_UserId",
                table: "ComponentsGuide");

            migrationBuilder.DropIndex(
                name: "IX_CalcVariants_UserId",
                table: "CalcVariants");

            migrationBuilder.DeleteData(
                table: "CalcVariants",
                keyColumn: "VariantId",
                keyValue: -2);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CalcVariants");
        }
    }
}

