using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBStructure.Migrations.SlagMode
{
    /// <inheritdoc />
    public partial class FixCalcData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BFId",
                table: "CalcVariants");

            migrationBuilder.RenameColumn(
                name: "CICrContent",
                table: "CalcVariants",
                newName: "CiCrContent");

            migrationBuilder.RenameColumn(
                name: "BFId",
                table: "CalcVariants",
                newName: "BfId");

            migrationBuilder.RenameIndex(
                name: "IX_CalcVariants_BFId",
                table: "CalcVariants",
                newName: "IX_CalcVariants_BfId");

            migrationBuilder.RenameColumn(
                name: "BFId",
                table: "BlastFurnaces",
                newName: "BfId");

            migrationBuilder.AddColumn<float>(
                name: "SlagAl2O3Content",
                table: "CalcVariants",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "SlagMgOContent",
                table: "CalcVariants",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.UpdateData(
                table: "CalcVariants",
                keyColumn: "VariantId",
                keyValue: -1,
                columns: new[] { "SlagAl2O3Content", "SlagMgOContent" },
                values: new object[] { 24.6f, 2f });

            migrationBuilder.AddForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BfId",
                table: "CalcVariants",
                column: "BfId",
                principalTable: "BlastFurnaces",
                principalColumn: "BfId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BfId",
                table: "CalcVariants");

            migrationBuilder.DropColumn(
                name: "SlagAl2O3Content",
                table: "CalcVariants");

            migrationBuilder.DropColumn(
                name: "SlagMgOContent",
                table: "CalcVariants");

            migrationBuilder.RenameColumn(
                name: "CiCrContent",
                table: "CalcVariants",
                newName: "CICrContent");

            migrationBuilder.RenameColumn(
                name: "BfId",
                table: "CalcVariants",
                newName: "BFId");

            migrationBuilder.RenameIndex(
                name: "IX_CalcVariants_BfId",
                table: "CalcVariants",
                newName: "IX_CalcVariants_BFId");

            migrationBuilder.RenameColumn(
                name: "BfId",
                table: "BlastFurnaces",
                newName: "BFId");

            migrationBuilder.AddForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BFId",
                table: "CalcVariants",
                column: "BFId",
                principalTable: "BlastFurnaces",
                principalColumn: "BFId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
