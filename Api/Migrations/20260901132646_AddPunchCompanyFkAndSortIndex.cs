using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    // Deux choses sur Punches : la FK manquante vers Companies (CompanyId n'était qu'une colonne
    // libre, rien n'empêchait un punch orphelin), et le remplacement de IX_Punches_CompanyId par
    // (CompanyId, PunchedAt DESC) qui couvre exactement le tri de PunchesController.GetAll.
    //
    // AddForeignKey échoue si des punchs référencent une entreprise inexistante. Aucun chemin de
    // code ne produit ça (le CompanyId vient du claim JWT), mais sur une base déjà en service,
    // vérifier d'abord :
    //   SELECT COUNT(*) FROM Punches p LEFT JOIN Companies c ON c.Id = p.CompanyId WHERE c.Id IS NULL
    public partial class AddPunchCompanyFkAndSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Punches_CompanyId",
                table: "Punches");

            migrationBuilder.CreateIndex(
                name: "IX_Punches_CompanyId_PunchedAt",
                table: "Punches",
                columns: new[] { "CompanyId", "PunchedAt" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "FK_Punches_Companies_CompanyId",
                table: "Punches",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Punches_Companies_CompanyId",
                table: "Punches");

            migrationBuilder.DropIndex(
                name: "IX_Punches_CompanyId_PunchedAt",
                table: "Punches");

            migrationBuilder.CreateIndex(
                name: "IX_Punches_CompanyId",
                table: "Punches",
                column: "CompanyId");
        }
    }
}
