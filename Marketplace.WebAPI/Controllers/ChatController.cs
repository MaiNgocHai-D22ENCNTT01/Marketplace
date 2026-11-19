using System.Linq.Expressions;
using Marketplace.Application.Chat;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Marketplace.WebAPI.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize(Roles = "CUSTOMER,SELLER"), EnableCors("ChatClient"), Route("api/chat")]
public sealed class ChatController(AppDbContext db, IHubContext<ChatHub> hub, ILogger<ChatController> logger) : ControllerBase
{
    private Guid Me => Guid.Parse(User.Identity!.Name!);
    private IQueryable<Conversation> Threads => db.Conversations.Where(c => c.CustomerId == Me || c.Shop.UserId == Me);

    [HttpGet("conversations")]
    public Task<List<ChatThread>> Conversations(CancellationToken ct) => Threads.AsNoTracking()
        .OrderByDescending(c => c.LastMessageAt).ThenBy(c => c.Id)
        .Select(c => new ChatThread(c.Id, c.CustomerId, c.ShopId, c.CustomerId == Me, c.CustomerId == Me ? c.Shop.Name : c.Customer.FullName,
            c.Messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Select(m => m.Content).FirstOrDefault(),
            c.LastMessageAt, c.Messages.Count(m => !m.IsRead && m.SenderId != Me))).ToListAsync(ct);

    private Expression<Func<ChatMessage, ChatEntry>> Entry(Guid viewer) => m => new ChatEntry(m.Id, m.ConversationId,
        m.SenderId == viewer, m.Content, m.Product == null ? null : new ChatProduct(m.Product.Id, m.Product.Name,
            m.Product.BasePrice, m.Product.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Id)
                .Select(i => i.ImageUrl).FirstOrDefault()), m.IsRead, m.CreatedAt);

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<ActionResult<List<ChatEntry>>> Messages(Guid id, Guid? before, CancellationToken ct)
    {
        if (!await Threads.AnyAsync(c => c.Id == id, ct)) return NotFound();
        var query = db.ChatMessages.Where(m => m.ConversationId == id);
        if (before is Guid cursor)
        {
            var anchor = await query.Where(m => m.Id == cursor).Select(m => (DateTime?)m.CreatedAt).SingleOrDefaultAsync(ct);
            if (anchor is null) return BadRequest();
            query = query.Where(m => m.CreatedAt < anchor || (m.CreatedAt == anchor && m.Id.CompareTo(cursor) < 0));
        }
        var rows = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(50).Select(Entry(Me)).ToListAsync(ct);
        rows.Reverse();
        return rows;
    }

    [HttpPost("conversations/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, [FromBody] Guid[] ids, CancellationToken ct)
    {
        if (ids.Length > 50) return BadRequest();
        if (!await Threads.AnyAsync(c => c.Id == id, ct)) return NotFound();
        await db.ChatMessages.Where(m => m.ConversationId == id && m.SenderId != Me && !m.IsRead && ids.Contains(m.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.IsRead, true), ct);
        return NoContent();
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatEntry>> Send(SendChat input, CancellationToken ct)
    {
        var content = input.Content?.Trim() ?? "";
        if (content.Length == 0 && input.ProductId is null) return BadRequest("Nhập tin nhắn hoặc chọn sản phẩm.");
        var me = Me;
        Guid customer, shopId;
        if (input.ConversationId is Guid threadId)
        {
            var pair = await Threads.Where(c => c.Id == threadId).Select(c => new { c.CustomerId, c.ShopId }).SingleOrDefaultAsync(ct);
            if (pair is null) return NotFound();
            customer = pair.CustomerId;
            shopId = pair.ShopId;
            if (input.ShopId.HasValue && input.ShopId != shopId) return BadRequest();
        }
        else
        {
            if (!User.IsInRole("CUSTOMER")) return Forbid();
            if (input.ShopId is not Guid target) return BadRequest("Thiếu gian hàng.");
            customer = me;
            shopId = target;
        }
        var shop = await db.Shops.Where(s => s.Id == shopId && s.Status == ShopStatus.ACTIVE)
            .Select(s => new { s.UserId }).SingleOrDefaultAsync(ct);
        if (shop is null) return NotFound();
        if (shop.UserId == customer) return BadRequest("Không thể nhắn cho chính mình.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var pairKey = $"chat/{customer:N}/{shopId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({pairKey}, 0))", ct);
        var thread = await db.Conversations.SingleOrDefaultAsync(c => c.CustomerId == customer && c.ShopId == shopId, ct);
        var messageId = input.MessageId ?? Guid.NewGuid();
        if (input.MessageId.HasValue)
        {
            var existing = await db.ChatMessages.Where(m => m.Id == messageId).Select(Entry(me)).SingleOrDefaultAsync(ct);
            if (existing is not null)
            {
                if (!existing.Mine || existing.ConversationId != thread?.Id || existing.Content != content ||
                    existing.Product?.Id != input.ProductId) return Conflict();
                return existing;
            }
        }
        if (input.ProductId is Guid pid && !await db.AvailableProducts().AnyAsync(p => p.Id == pid && p.ShopId == shopId, ct))
            return BadRequest("Sản phẩm không thuộc gian hàng hoặc đã ngừng bán.");
        if (thread is null)
        {
            thread = new Conversation { Id = Guid.NewGuid(), CustomerId = customer, ShopId = shopId };
            db.Conversations.Add(thread);
        }
        thread.LastMessageAt = DateTime.UtcNow;
        db.ChatMessages.Add(new ChatMessage { Id = messageId, ConversationId = thread.Id, SenderId = me,
            Content = content, ProductId = input.ProductId, CreatedAt = thread.LastMessageAt });
        await db.SaveChangesAsync(ct);
        var entry = await db.ChatMessages.Where(m => m.Id == messageId).Select(Entry(me)).SingleAsync(ct);
        await tx.CommitAsync(ct);
        try
        {
            await hub.Clients.Users(customer.ToString(), shop.UserId.ToString())
                .SendAsync("MessageReceived", new { conversationId = thread.Id, messageId });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not notify chat participants for message {MessageId}", messageId);
        }
        return entry;
    }
}
