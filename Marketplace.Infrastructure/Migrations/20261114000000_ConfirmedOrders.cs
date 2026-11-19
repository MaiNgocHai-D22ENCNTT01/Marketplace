using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Marketplace.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261114000000_ConfirmedOrders")]
public sealed class ConfirmedOrders : Migration
{
    private const string Legacy = "'PENDING', 'READY_FOR_PICKUP', 'SHIPPING', 'DELIVERED', 'COMPLETED', 'CANCELLED', 'RETURN_REQUESTED', 'RETURNED'";

    protected override void Up(MigrationBuilder migrationBuilder) => SetConstraint(migrationBuilder, Legacy + ", 'CONFIRMED'");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE orders SET order_status='READY_FOR_PICKUP' WHERE order_status='CONFIRMED'");
        SetConstraint(migrationBuilder, Legacy);
    }

    private static void SetConstraint(MigrationBuilder migration, string statuses)
    {
        migration.DropCheckConstraint("ck_orders_order_status", "orders");
        migration.AddCheckConstraint("ck_orders_order_status", "orders", $"order_status IN ({statuses})");
    }
}
