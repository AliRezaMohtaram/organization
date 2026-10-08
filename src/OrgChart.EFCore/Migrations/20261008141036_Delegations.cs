using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrgChart.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class Delegations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Delegations",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ToPositionId = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsFullScope = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => x.Id);
                    table.CheckConstraint("CK_Delegations_Target", "([Kind] = 1 AND [FromUserId] IS NOT NULL AND [ToUserId] IS NOT NULL AND [ToPositionId] IS NULL AND [ValidTo] IS NOT NULL) OR ([Kind] = 2 AND [FromUserId] IS NULL AND [ToUserId] IS NULL AND [ToPositionId] IS NOT NULL)");
                    table.CheckConstraint("CK_Delegations_ValidRange", "[ValidFrom] IS NULL OR [ValidTo] IS NULL OR [ValidFrom] <= [ValidTo]");
                    table.ForeignKey(
                        name: "FK_Delegations_Positions_PositionId",
                        column: x => x.PositionId,
                        principalSchema: "org",
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Delegations_Positions_ToPositionId",
                        column: x => x.ToPositionId,
                        principalSchema: "org",
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DelegationScopes",
                schema: "org",
                columns: table => new
                {
                    DelegationId = table.Column<int>(type: "int", nullable: false),
                    AuthorityKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationScopes", x => new { x.DelegationId, x.AuthorityKey });
                    table.ForeignKey(
                        name: "FK_DelegationScopes_Delegations_DelegationId",
                        column: x => x.DelegationId,
                        principalSchema: "org",
                        principalTable: "Delegations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_PositionId",
                schema: "org",
                table: "Delegations",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_ToPositionId",
                schema: "org",
                table: "Delegations",
                column: "ToPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_ToUserId",
                schema: "org",
                table: "Delegations",
                column: "ToUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegationScopes",
                schema: "org");

            migrationBuilder.DropTable(
                name: "Delegations",
                schema: "org");
        }
    }
}
