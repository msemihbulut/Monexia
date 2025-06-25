using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monexia.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGoalType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Goals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Goals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
