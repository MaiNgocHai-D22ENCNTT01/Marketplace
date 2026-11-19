using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_status", "status IN ('ACTIVE', 'INACTIVE', 'BANNED')");
        });
        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        entity.Property(x => x.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.HasIndex(x => x.Email).IsUnique();

        entity.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.FullName)
            .HasColumnName("full_name")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20);

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(UserStatus.ACTIVE)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();
    }
}
