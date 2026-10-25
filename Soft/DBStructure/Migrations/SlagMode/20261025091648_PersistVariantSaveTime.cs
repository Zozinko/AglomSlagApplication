using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBStructure.Migrations.SlagMode
{
    /// <inheritdoc />
    public partial class PersistVariantSaveTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SaveTime",
                table: "CalcVariants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CalcVariants",
                keyColumn: "VariantId",
                keyValue: -1,
                column: "SaveTime",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaveTime",
                table: "CalcVariants");
        }
    }
}
