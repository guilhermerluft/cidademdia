using CidadeEmDia.Domain.Chat;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.ToTable("chat_conversations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OccurrenceId)
            .HasColumnName("occurrence_id")
            .IsRequired();
        builder.Property(x => x.OccurrenceTargetId)
            .HasColumnName("occurrence_target_id")
            .IsRequired();
        builder.Property(x => x.CitizenUserId)
            .HasColumnName("citizen_user_id")
            .IsRequired();
        builder.Property(x => x.MasterUserId)
            .HasColumnName("master_user_id")
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(x => x.ClosedAt)
            .HasColumnName("closed_at");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.OccurrenceTargetId).IsUnique();
        builder.HasIndex(x => x.OccurrenceId);
        builder.HasIndex(x => x.CitizenUserId);
        builder.HasIndex(x => x.MasterUserId);
        builder.HasIndex(x => x.Status);

        builder.HasOne<OccurrenceTarget>()
            .WithOne()
            .HasForeignKey<ChatConversation>(x => x.OccurrenceTargetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CitizenUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.MasterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Participants)
            .WithOne()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Participants)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
