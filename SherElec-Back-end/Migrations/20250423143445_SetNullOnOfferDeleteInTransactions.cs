using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SherElec_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class SetNullOnOfferDeleteInTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Offers_OffreId",
                table: "Transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Offers_OffreId",
                table: "Transactions",
                column: "OffreId",
                principalTable: "Offers",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Offers_OffreId",
                table: "Transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Offers_OffreId",
                table: "Transactions",
                column: "OffreId",
                principalTable: "Offers",
                principalColumn: "ID");
        }
    }
}
