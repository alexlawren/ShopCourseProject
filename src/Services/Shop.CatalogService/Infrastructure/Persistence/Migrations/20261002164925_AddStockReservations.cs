using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.CatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockReservationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservationItems", x => x.Id);
                    table.CheckConstraint("CK_StockReservationItems_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_StockReservationItems_UnitPrice", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockReservationItems_StockReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "StockReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservationItems_ReservationId",
                table: "StockReservationItems",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservationItems_ReservationId_ProductId",
                table: "StockReservationItems",
                columns: new[] { "ReservationId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_CreatedAtUtc",
                table: "StockReservations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_RequestId",
                table: "StockReservations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_Status",
                table: "StockReservations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockReservationItems");

            migrationBuilder.DropTable(
                name: "StockReservations");
        }
    }
}
