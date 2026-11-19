using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> entity)
    {
        entity.ToTable("cart_items", table =>
        {
            table.HasCheckConstraint("ck_cart_items_quantity", "quantity > 0");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.CartId)
            .HasColumnName("cart_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.VariantId)
            .HasColumnName("variant_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("int")
            .HasDefaultValue(1)
            .HasSentinel(-1)
            .IsRequired();

        entity.HasOne(x => x.Cart)
            .WithMany(x => x.CartItems)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Variant)
            .WithMany(x => x.CartItems)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
