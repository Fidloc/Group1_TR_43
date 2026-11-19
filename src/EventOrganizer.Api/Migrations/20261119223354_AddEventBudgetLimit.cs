using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventOrganizer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventBudgetLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BudgetLimit",
                table: "Events",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetLimit",
                table: "Events");
        }
    }
}
