using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Configurations;

public sealed class ChatConfiguration : IEntityTypeConfiguration<Conversation>, IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<Conversation> e)
    {
        e.ToTable("conversations");
        e.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        e.Property(c => c.CustomerId).HasColumnName("customer_id");
        e.Property(c => c.ShopId).HasColumnName("shop_id");
        e.Property(c => c.LastMessageAt).HasColumnName("last_message_at");
        e.HasIndex(c => new { c.CustomerId, c.ShopId }).IsUnique();
        e.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId);
        e.HasOne(c => c.Shop).WithMany().HasForeignKey(c => c.ShopId);
    }

    public void Configure(EntityTypeBuilder<ChatMessage> e)
    {
        e.ToTable("chat_messages");
        e.Property(m => m.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        e.Property(m => m.ConversationId).HasColumnName("conversation_id");
        e.Property(m => m.SenderId).HasColumnName("sender_id");
        e.Property(m => m.Content).HasColumnName("content").HasMaxLength(4000);
        e.Property(m => m.ProductId).HasColumnName("product_id");
        e.Property(m => m.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        e.Property(m => m.CreatedAt).HasColumnName("created_at");
        e.HasIndex(m => new { m.ConversationId, m.CreatedAt, m.Id });
        e.HasOne<Conversation>().WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId);
        e.HasOne<User>().WithMany().HasForeignKey(m => m.SenderId);
        e.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}
