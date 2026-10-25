using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBStructure.Migrations.SlagMode
{
    /// <inheritdoc />
    public partial class AddSlagSulfurContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "SlagSContent",
                table: "CalcVariants",
                type: "real",
                nullable: false,
                defaultValue: 0f);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlagSContent",
                table: "CalcVariants");
        }
    }
}
