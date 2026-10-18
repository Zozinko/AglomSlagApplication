using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBStructure.Migrations.SlagMode
{
    /// <inheritdoc />
    public partial class EditReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BfId",
                table: "CalcVariants");

            migrationBuilder.AddForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BfId",
                table: "CalcVariants",
                column: "BfId",
                principalTable: "BlastFurnaces",
                principalColumn: "BfId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BfId",
                table: "CalcVariants");

            migrationBuilder.AddForeignKey(
                name: "FK_CalcVariants_BlastFurnaces_BfId",
                table: "CalcVariants",
                column: "BfId",
                principalTable: "BlastFurnaces",
                principalColumn: "BfId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
