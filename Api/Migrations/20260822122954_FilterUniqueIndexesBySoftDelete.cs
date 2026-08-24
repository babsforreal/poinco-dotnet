using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class FilterUniqueIndexesBySoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_CardUid",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_EmployeeNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_Pin",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_CardUid",
                table: "Employees",
                columns: new[] { "CompanyId", "CardUid" },
                unique: true,
                filter: "[CardUid] IS NOT NULL AND [DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_EmployeeNumber",
                table: "Employees",
                columns: new[] { "CompanyId", "EmployeeNumber" },
                unique: true,
                filter: "[EmployeeNumber] IS NOT NULL AND [DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_Pin",
                table: "Employees",
                columns: new[] { "CompanyId", "Pin" },
                unique: true,
                filter: "[DeletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_CardUid",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_EmployeeNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_Pin",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_CardUid",
                table: "Employees",
                columns: new[] { "CompanyId", "CardUid" },
                unique: true,
                filter: "[CardUid] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_EmployeeNumber",
                table: "Employees",
                columns: new[] { "CompanyId", "EmployeeNumber" },
                unique: true,
                filter: "[EmployeeNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_Pin",
                table: "Employees",
                columns: new[] { "CompanyId", "Pin" },
                unique: true);
        }
    }
}
