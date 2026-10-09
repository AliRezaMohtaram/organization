using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgChart.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class PositionHierarchyAndTypeLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentPositionId",
                schema: "org",
                table: "Positions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanBeRoot",
                schema: "org",
                table: "OrgUnitTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                schema: "org",
                table: "OrgUnitTypes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_ParentPositionId",
                schema: "org",
                table: "Positions",
                column: "ParentPositionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrgUnitTypes_Level",
                schema: "org",
                table: "OrgUnitTypes",
                sql: "[Level] IS NULL OR [Level] >= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Positions_ParentPositionId",
                schema: "org",
                table: "Positions",
                column: "ParentPositionId",
                principalSchema: "org",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Positions_ParentPositionId",
                schema: "org",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Positions_ParentPositionId",
                schema: "org",
                table: "Positions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrgUnitTypes_Level",
                schema: "org",
                table: "OrgUnitTypes");

            migrationBuilder.DropColumn(
                name: "ParentPositionId",
                schema: "org",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "CanBeRoot",
                schema: "org",
                table: "OrgUnitTypes");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "org",
                table: "OrgUnitTypes");
        }
    }
}
