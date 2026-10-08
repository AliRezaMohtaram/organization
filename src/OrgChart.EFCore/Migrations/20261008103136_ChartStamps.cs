using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgChart.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class ChartStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartStamps",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Stamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartStamps", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "org",
                table: "ChartStamps",
                columns: new[] { "Id", "Stamp", "UpdatedAt" },
                values: new object[] { 1, new Guid("5d0f3c9e-2f6b-4b8e-9a51-0c7f6e1d2a10"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartStamps",
                schema: "org");
        }
    }
}
