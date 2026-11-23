using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_385.Migrations
{
    /// <inheritdoc />
    public partial class Add_Product_Stock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockQuantity",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "StockReturned",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Orders that existed before stock tracking did not decrement inventory.
            migrationBuilder.Sql("UPDATE [Orders] SET [StockReturned] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StockReturned",
                table: "Orders");
        }
    }
}
