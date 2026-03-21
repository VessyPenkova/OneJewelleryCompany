using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneJevelsCompany.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTypeToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                table: "Orders",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "Orders");
        }
    }
}
