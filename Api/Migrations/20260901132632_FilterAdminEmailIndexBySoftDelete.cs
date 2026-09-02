using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    // Aligne IX_Admins_Email sur ce que la migration FilterUniqueIndexesBySoftDelete avait déjà
    // fait pour les index Employee : sans filtre, un email libéré par soft-delete restait bloqué
    // en base alors que le HasQueryFilter le rendait invisible côté application.
    //
    // Le Up() ne fait que relâcher la contrainte, il ne peut donc pas échouer sur des données
    // existantes. Le Down(), lui, PEUT échouer : dès qu'un email a été réutilisé après
    // soft-delete (le scénario que ce Up autorise), restaurer l'index non filtré viole l'unicité.
    // Un rollback demandera de purger ou renommer les lignes DeletedAt IS NOT NULL concernées.
    public partial class FilterAdminEmailIndexBySoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Admins_Email",
                table: "Admins");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Email",
                table: "Admins",
                column: "Email",
                unique: true,
                filter: "[DeletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Admins_Email",
                table: "Admins");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Email",
                table: "Admins",
                column: "Email",
                unique: true);
        }
    }
}
