using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashSaleFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "flash_sale_item_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "flash_sale_campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flash_sale_campaigns", x => x.id);
                    table.CheckConstraint("ck_flash_status", "status IN ('UPCOMING','ACTIVE','ENDED')");
                    table.CheckConstraint("ck_flash_window", "end_time > start_time");
                });

            migrationBuilder.CreateTable(
                name: "flash_sale_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flash_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_quota = table.Column<int>(type: "integer", nullable: false),
                    sold_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_per_user = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flash_sale_items", x => x.id);
                    table.CheckConstraint("ck_flash_item_status", "status IN ('PENDING','APPROVED','REJECTED')");
                    table.CheckConstraint("ck_flash_price", "flash_price > 0");
                    table.CheckConstraint("ck_flash_quota", "total_quota > 0 AND sold_count BETWEEN 0 AND total_quota AND max_per_user BETWEEN 1 AND total_quota");
                    table.ForeignKey(
                        name: "FK_flash_sale_items_flash_sale_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "flash_sale_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_flash_sale_items_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flash_sale_items_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_flash_sale_item_id",
                table: "order_items",
                column: "flash_sale_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_flash_sale_campaigns_start_time",
                table: "flash_sale_campaigns",
                column: "start_time");

            migrationBuilder.CreateIndex(
                name: "IX_flash_sale_items_campaign_id_variant_id",
                table: "flash_sale_items",
                columns: new[] { "campaign_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flash_sale_items_shop_id",
                table: "flash_sale_items",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "IX_flash_sale_items_variant_id",
                table: "flash_sale_items",
                column: "variant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_flash_sale_items_flash_sale_item_id",
                table: "order_items",
                column: "flash_sale_item_id",
                principalTable: "flash_sale_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_items_flash_sale_items_flash_sale_item_id",
                table: "order_items");

            migrationBuilder.DropTable(
                name: "flash_sale_items");

            migrationBuilder.DropTable(
                name: "flash_sale_campaigns");

            migrationBuilder.DropIndex(
                name: "IX_order_items_flash_sale_item_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "flash_sale_item_id",
                table: "order_items");
        }
    }
}
