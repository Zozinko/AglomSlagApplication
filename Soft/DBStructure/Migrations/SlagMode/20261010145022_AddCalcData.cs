using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DBStructure.Migrations.SlagMode
{
    /// <inheritdoc />
    public partial class AddCalcData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "BlastFurnaces",
                columns: new[] { "BFId", "BfName" },
                values: new object[] { -1, "TestBF" });

            migrationBuilder.InsertData(
                table: "ComponentsGuide",
                columns: new[] { "ComponentId", "Al2O3Content", "CaOContent", "ComponentName", "FeContent", "MgOContent", "MnOContent", "SContent", "SiO2Content", "TiO2Content", "UserId" },
                values: new object[,]
                {
                    { -8, 2f, 8.5f, "Королёк", 68.4f, 3.1f, 0.98f, 0.11f, 6.8f, 0.34f, -1 },
                    { -7, 0.74f, 0.25f, "Сварочный шлак", 69.8f, 0.39f, 0.84f, 0.02f, 4.3f, 0f, -1 },
                    { -6, 0.23f, 1.49f, "МихГОК", 63.3f, 0.25f, 0.04f, 0.01f, 7.25f, 0f, -1 },
                    { -5, 2.59f, 1.28f, "КачГОК", 60.4f, 2.9f, 0.23f, 0.02f, 4.36f, 2.66f, -1 },
                    { -4, 0.25f, 0.4f, "ЛебГОК", 65.7f, 0.22f, 0.05f, 0.01f, 5.17f, 0f, -1 },
                    { -3, 1.21f, 4.02f, "окатыши ССГПО", 62.6f, 0.99f, 0.16f, 0.067f, 3.7f, 0.32f, -1 },
                    { -2, 1.76f, 8.86f, "агломерат а/ф № 4", 58.3f, 1.64f, 0.19f, 0.028f, 5.95f, 0.24f, -1 },
                    { -1, 1.75f, 8.72f, "агломерат а/ф № 2 и 3", 58.5f, 1.63f, 0.19f, 0.028f, 5.88f, 0.24f, -1 }
                });

            migrationBuilder.InsertData(
                table: "CalcVariants",
                columns: new[] { "VariantId", "BFId", "CICrContent", "CalcDate", "CiCContent", "CiMnContent", "CiSContent", "CiSiContent", "CiTemperature", "CiTiContent", "CokeAshAl2O3Content", "CokeAshAmount", "CokeAshCaOContent", "CokeAshMgOContent", "CokeAshSiO2Content", "CokeConsumption", "CokeSContent", "IsImport", "ParentId", "SlagCaOContent", "SlagSiO2Content", "SlagTiO2Content", "VariantName" },
                values: new object[] { -1, -1, 0f, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4.702f, 0.2f, 0.016f, 0.512f, 1450f, 0f, 24.6f, 12.7f, 7.8f, 2f, 48.1f, 419.8f, 0.428f, false, null, 40.9f, 36.56f, 0.01f, "-1st variant" });

            migrationBuilder.InsertData(
                table: "ComponentVariants",
                columns: new[] { "ComponentId", "VariantId", "Al2O3Content", "CaOContent", "ComponentName", "Consumption", "FeContent", "MgOContent", "MnOContent", "SContent", "SiO2Content", "TiO2Content" },
                values: new object[,]
                {
                    { -6, -1, 0.23f, 1.49f, "МихГОК", 48.5f, 63.3f, 0.25f, 0.04f, 0.01f, 7.25f, 0f },
                    { -5, -1, 2.59f, 1.28f, "КачГОК", 54.1f, 60.4f, 2.9f, 0.23f, 0.02f, 4.36f, 2.66f },
                    { -4, -1, 0.25f, 0.4f, "ЛебГОК", 54.7f, 65.7f, 0.22f, 0.05f, 0.01f, 5.17f, 0f },
                    { -3, -1, 1.21f, 4.02f, "окатыши ССГПО", 568.7f, 62.6f, 0.99f, 0.16f, 0.067f, 3.7f, 0.32f },
                    { -2, -1, 1.76f, 8.86f, "агломерат а/ф № 4", 485.9f, 58.3f, 1.64f, 0.19f, 0.028f, 5.95f, 0.24f },
                    { -1, -1, 1.75f, 8.72f, "агломерат а/ф № 2 и 3", 441.5f, 58.5f, 1.63f, 0.19f, 0.028f, 5.88f, 0.24f }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ComponentVariants",
                keyColumns: new[] { "ComponentId", "VariantId" },
                keyValues: new object[] { -6, -1 });

            migrationBuilder.DeleteData(
                table: "ComponentVariants",
                keyColumns: new[] { "ComponentId", "VariantId" },
                keyValues: new object[] { -5, -1 });

            migrationBuilder.DeleteData(
                table: "ComponentVariants",
                keyColumns: new[] { "ComponentId", "VariantId" },
                keyValues: new object[] { -4, -1 });

            migrationBuilder.DeleteData(
                table: "ComponentVariants",
                keyColumns: new[] { "ComponentId", "VariantId" },
                keyValues: new object[] { -3, -1 });

            migrationBuilder.DeleteData(
                table: "ComponentVariants",
                keyColumns: new[] { "ComponentId", "VariantId" },
                keyValues: new object[] { -2, -1 });

            migrationBuilder.DeleteData(
                table: "ComponentVariants",
                keyColumns: new[] { "ComponentId", "VariantId" },
                keyValues: new object[] { -1, -1 });

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -8);

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -7);

            migrationBuilder.DeleteData(
                table: "CalcVariants",
                keyColumn: "VariantId",
                keyValue: -1);

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -6);

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -5);

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -4);

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -3);

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -2);

            migrationBuilder.DeleteData(
                table: "ComponentsGuide",
                keyColumn: "ComponentId",
                keyValue: -1);

            migrationBuilder.DeleteData(
                table: "BlastFurnaces",
                keyColumn: "BFId",
                keyValue: -1);
        }
    }
}
