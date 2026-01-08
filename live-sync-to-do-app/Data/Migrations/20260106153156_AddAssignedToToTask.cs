using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace live_sync_to_do_app.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedToToTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedTo",
                table: "TodoTasks",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "TodoTasks");
        }
    }
}
