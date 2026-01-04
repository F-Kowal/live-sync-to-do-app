using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace live_sync_to_do_app.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDescriptionAndDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TodoTasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "TodoTasks",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "TodoTasks");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "TodoTasks");
        }
    }
}
