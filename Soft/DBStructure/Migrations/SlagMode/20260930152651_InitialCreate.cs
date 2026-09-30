using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DBStructure.Migrations.SlagMode
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlastFurnaces",
                columns: table => new
                {
                    BFId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BfName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlastFurnaces", x => x.BFId);
                });

            migrationBuilder.CreateTable(
                name: "ComponentsGuide",
                columns: table => new
                {
                    ComponentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponentName = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FeContent = table.Column<float>(type: "real", nullable: false),
                    SiO2Content = table.Column<float>(type: "real", nullable: false),
                    Al2O3Content = table.Column<float>(type: "real", nullable: false),
                    CaOContent = table.Column<float>(type: "real", nullable: false),
                    MgOContent = table.Column<float>(type: "real", nullable: false),
                    SContent = table.Column<float>(type: "real", nullable: false),
                    MnOContent = table.Column<float>(type: "real", nullable: false),
                    TiO2Content = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentsGuide", x => x.ComponentId);
                });

            migrationBuilder.CreateTable(
                name: "CalcVariants",
                columns: table => new
                {
                    VariantId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BFId = table.Column<int>(type: "integer", nullable: false),
                    CalcDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VariantName = table.Column<string>(type: "text", nullable: false),
                    CokeConsumption = table.Column<float>(type: "real", nullable: false),
                    CokeSContent = table.Column<float>(type: "real", nullable: false),
                    CokeAshAmount = table.Column<float>(type: "real", nullable: false),
                    CokeAshCaOContent = table.Column<float>(type: "real", nullable: false),
                    CokeAshSiO2Content = table.Column<float>(type: "real", nullable: false),
                    CokeAshAl2O3Content = table.Column<float>(type: "real", nullable: false),
                    CokeAshMgOContent = table.Column<float>(type: "real", nullable: false),
                    SlagCaOContent = table.Column<float>(type: "real", nullable: false),
                    SlagSiO2Content = table.Column<float>(type: "real", nullable: false),
                    SlagTiO2Content = table.Column<float>(type: "real", nullable: false),
                    CiTemperature = table.Column<float>(type: "real", nullable: false),
                    CiSiContent = table.Column<float>(type: "real", nullable: false),
                    CiSContent = table.Column<float>(type: "real", nullable: false),
                    CiMnContent = table.Column<float>(type: "real", nullable: false),
                    CiCContent = table.Column<float>(type: "real", nullable: false),
                    CiTiContent = table.Column<float>(type: "real", nullable: false),
                    CICrContent = table.Column<float>(type: "real", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalcVariants", x => x.VariantId);
                    table.ForeignKey(
                        name: "FK_CalcVariants_BlastFurnaces_BFId",
                        column: x => x.BFId,
                        principalTable: "BlastFurnaces",
                        principalColumn: "BFId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalcVariants_CalcVariants_ParentId",
                        column: x => x.ParentId,
                        principalTable: "CalcVariants",
                        principalColumn: "VariantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComponentVariants",
                columns: table => new
                {
                    VariantId = table.Column<int>(type: "integer", nullable: false),
                    ComponentId = table.Column<int>(type: "integer", nullable: false),
                    ComponentName = table.Column<string>(type: "text", nullable: false),
                    Consumption = table.Column<float>(type: "real", nullable: false),
                    FeContent = table.Column<float>(type: "real", nullable: false),
                    SiO2Content = table.Column<float>(type: "real", nullable: false),
                    Al2O3Content = table.Column<float>(type: "real", nullable: false),
                    CaOContent = table.Column<float>(type: "real", nullable: false),
                    MgOContent = table.Column<float>(type: "real", nullable: false),
                    SContent = table.Column<float>(type: "real", nullable: false),
                    MnOContent = table.Column<float>(type: "real", nullable: false),
                    TiO2Content = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentVariants", x => new { x.VariantId, x.ComponentId });
                    table.ForeignKey(
                        name: "FK_ComponentVariants_CalcVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "CalcVariants",
                        principalColumn: "VariantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComponentVariants_ComponentsGuide_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "ComponentsGuide",
                        principalColumn: "ComponentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalcVariants_BFId",
                table: "CalcVariants",
                column: "BFId");

            migrationBuilder.CreateIndex(
                name: "IX_CalcVariants_ParentId",
                table: "CalcVariants",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentVariants_ComponentId",
                table: "ComponentVariants",
                column: "ComponentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponentVariants");

            migrationBuilder.DropTable(
                name: "CalcVariants");

            migrationBuilder.DropTable(
                name: "ComponentsGuide");

            migrationBuilder.DropTable(
                name: "BlastFurnaces");
        }
    }
}
