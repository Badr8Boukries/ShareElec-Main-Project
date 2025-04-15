using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SherElec_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionsDbSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAcheteur = table.Column<int>(type: "int", nullable: false),
                    IdVendeur = table.Column<int>(type: "int", nullable: false),
                    Quantite = table.Column<double>(type: "float", nullable: false),
                    PrixUnitaire = table.Column<double>(type: "float", nullable: false),
                    PrixTotal = table.Column<double>(type: "float", nullable: false),
                    DateTransaction = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OffreId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Transactions_Offers_OffreId",
                        column: x => x.OffreId,
                        principalTable: "Offers",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Transactions_Users_IdAcheteur",
                        column: x => x.IdAcheteur,
                        principalTable: "Users",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Transactions_Users_IdVendeur",
                        column: x => x.IdVendeur,
                        principalTable: "Users",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdAcheteur",
                table: "Transactions",
                column: "IdAcheteur");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdVendeur",
                table: "Transactions",
                column: "IdVendeur");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OffreId",
                table: "Transactions",
                column: "OffreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
