using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.OrderService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCancellationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationState",
                table: "Orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationState",
                table: "Orders");
        }
    }
}
