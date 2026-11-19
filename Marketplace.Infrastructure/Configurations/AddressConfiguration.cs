using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> entity)
    {
        entity.ToTable("addresses");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        entity.Property(x => x.RecipientName)
            .HasColumnName("recipient_name")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.Province)
            .HasColumnName("province")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.District)
            .HasColumnName("district")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.Ward)
            .HasColumnName("ward")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.DetailAddress)
            .HasColumnName("detail_address")
            .HasColumnType("text")
            .IsRequired();

        entity.Property(x => x.IsDefault)
            .HasColumnName("is_default")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        entity.HasOne(x => x.User)
            .WithMany(x => x.Addresses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
