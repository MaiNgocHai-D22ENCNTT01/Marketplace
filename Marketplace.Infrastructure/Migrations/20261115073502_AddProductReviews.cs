using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs");

            migrationBuilder.DropIndex(
                name: "IX_reviews_product_id",
                table: "reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviews_status",
                table: "reviews");

            migrationBuilder.AddColumn<int>(
                name: "review_count",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "reviews",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ACTIVE",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "VISIBLE");

            migrationBuilder.AddColumn<string[]>(
                name: "images",
                table: "reviews",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<decimal>(
                name: "rating_avg",
                table: "products",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "review_count",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE reviews SET status='ACTIVE' WHERE status='VISIBLE';
                UPDATE products p SET (rating_avg,review_count) =
                    (SELECT coalesce(avg(rating),0),count(*) FROM reviews WHERE product_id=p.id AND status='ACTIVE');
                UPDATE shops s SET (rating_avg,review_count) =
                    (SELECT coalesce(avg(r.rating),0),count(*) FROM reviews r JOIN products p ON p.id=r.product_id
                     WHERE p.shop_id=s.id AND r.status='ACTIVE');
                """);
            migrationBuilder.AddCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs",
                sql: "action_type IN ('VIEW', 'CLICK', 'ADD_TO_CART', 'PURCHASE', 'WISHLIST_ADD', 'WISHLIST_REMOVE', 'REVIEW')");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_product_id_status_created_at",
                table: "reviews",
                columns: new[] { "product_id", "status", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviews_status",
                table: "reviews",
                sql: "status IN ('ACTIVE', 'HIDDEN')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs");

            migrationBuilder.DropIndex(
                name: "IX_reviews_product_id_status_created_at",
                table: "reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviews_status",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "review_count",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "images",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "rating_avg",
                table: "products");

            migrationBuilder.DropColumn(
                name: "review_count",
                table: "products");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "reviews",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "VISIBLE",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "ACTIVE");

            migrationBuilder.Sql("UPDATE reviews SET status='VISIBLE' WHERE status='ACTIVE'");
            migrationBuilder.Sql("DELETE FROM user_interaction_logs WHERE action_type='REVIEW'");
            migrationBuilder.AddCheckConstraint(
                name: "ck_user_interaction_logs_action_type",
                table: "user_interaction_logs",
                sql: "action_type IN ('VIEW', 'CLICK', 'ADD_TO_CART', 'PURCHASE', 'WISHLIST_ADD', 'WISHLIST_REMOVE')");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_product_id",
                table: "reviews",
                column: "product_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviews_status",
                table: "reviews",
                sql: "status IN ('VISIBLE', 'HIDDEN')");
        }
    }
}
