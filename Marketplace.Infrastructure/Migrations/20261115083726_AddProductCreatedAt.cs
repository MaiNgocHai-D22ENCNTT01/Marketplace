using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_shop_id",
                table: "products");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateIndex(
                name: "IX_products_shop_id_created_at_id",
                table: "products",
                columns: new[] { "shop_id", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_shop_id_created_at_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "products");

            migrationBuilder.CreateIndex(
                name: "IX_products_shop_id",
                table: "products",
                column: "shop_id");
        }
    }
}
