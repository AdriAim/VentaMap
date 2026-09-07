using Microsoft.EntityFrameworkCore;
using VentaMap.ChatService.Models;

namespace VentaMap.ChatService.Data;

public class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatConversation>().ToTable("ChatConversations");
        modelBuilder.Entity<ChatMessage>().ToTable("ChatMessages");

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => new { x.PublicationId, x.BuyerUserId, x.SellerUserId })
            .IsUnique();

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => new { x.BuyerUserId, x.LastMessageAtUtc });

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => new { x.SellerUserId, x.LastMessageAtUtc });

        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => new { x.ConversationId, x.ReadAtUtc });

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => new { x.EmailReminderSentAtUtc, x.CreatedAtUtc });
    }
}
