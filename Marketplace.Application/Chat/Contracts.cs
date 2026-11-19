using System.ComponentModel.DataAnnotations;

namespace Marketplace.Application.Chat;

public record SendChat(Guid? ConversationId, Guid? ShopId, [param: MaxLength(4000)] string? Content,
    Guid? ProductId, Guid? MessageId);
public record ChatThread(Guid Id, Guid CustomerId, Guid ShopId, bool AsCustomer, string OtherName, string? Preview,
    DateTime LastMessageAt, int Unread);
public record ChatProduct(Guid Id, string Name, decimal Price, string? ImageUrl);
public record ChatEntry(Guid Id, Guid ConversationId, bool Mine, string Content,
    ChatProduct? Product, bool IsRead, DateTime CreatedAt);
