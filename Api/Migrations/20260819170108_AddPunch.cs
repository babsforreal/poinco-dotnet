using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPunch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shift_Companies_CompanyId",
                table: "Shift");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shift",
                table: "Shift");

            migrationBuilder.RenameTable(
                name: "Shift",
                newName: "Shifts");

            migrationBuilder.RenameIndex(
                name: "IX_Shift_CompanyId",
                table: "Shifts",
                newName: "IX_Shifts_CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shifts",
                table: "Shifts",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Punches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CompanyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PunchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RawPunchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Punches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Punches_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Punches_CompanyId",
                table: "Punches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Punches_EmployeeId_PunchedAt",
                table: "Punches",
                columns: new[] { "EmployeeId", "PunchedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Companies_CompanyId",
                table: "Shifts",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Companies_CompanyId",
                table: "Shifts");

            migrationBuilder.DropTable(
                name: "Punches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shifts",
                table: "Shifts");

            migrationBuilder.RenameTable(
                name: "Shifts",
                newName: "Shift");

            migrationBuilder.RenameIndex(
                name: "IX_Shifts_CompanyId",
                table: "Shift",
                newName: "IX_Shift_CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shift",
                table: "Shift",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shift_Companies_CompanyId",
                table: "Shift",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
