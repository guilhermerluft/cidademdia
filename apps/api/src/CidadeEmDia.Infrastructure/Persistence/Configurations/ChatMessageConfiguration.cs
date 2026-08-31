using CidadeEmDia.Domain.Chat;
using CidadeEmDia.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Sequence)
            .HasColumnName("sequence")
            .UseIdentityByDefaultColumn()
            .ValueGeneratedOnAdd();
        builder.Property(x => x.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();
        builder.Property(x => x.SenderUserId)
            .HasColumnName("sender_user_id")
            .IsRequired();
        builder.Property(x => x.ClientMessageId)
            .HasColumnName("client_message_id")
            .IsRequired();
        builder.Property(x => x.Content)
            .HasColumnName("content")
            .HasMaxLength(ChatMessage.MaxTextLength)
            .IsRequired();
        builder.Property(x => x.SentAt)
            .HasColumnName("sent_at")
            .IsRequired();

        builder.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.ConversationId, x.SenderUserId, x.ClientMessageId }).IsUnique();
        builder.HasIndex(x => x.SenderUserId);

        builder.HasOne<ChatConversation>()
            .WithMany()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
