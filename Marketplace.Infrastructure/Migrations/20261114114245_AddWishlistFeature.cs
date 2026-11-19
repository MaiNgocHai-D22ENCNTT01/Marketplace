using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs");

            migrationBuilder.CreateTable(
                name: "wishlist_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlist_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_wishlist_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wishlist_items_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs",
                sql: "action_type IN ('VIEW', 'CLICK', 'ADD_TO_CART', 'PURCHASE', 'WISHLIST_ADD', 'WISHLIST_REMOVE')");

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_product_id",
                table: "wishlist_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_user_id_product_id",
                table: "wishlist_items",
                columns: new[] { "user_id", "product_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM user_interaction_logs WHERE action_type IN ('WISHLIST_ADD', 'WISHLIST_REMOVE')");
            migrationBuilder.DropTable(
                name: "wishlist_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs",
                sql: "action_type IN ('VIEW', 'CLICK', 'ADD_TO_CART', 'PURCHASE')");
        }
    }
}
