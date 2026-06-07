using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class TcgDeckConfiguration : IEntityTypeConfiguration<TcgDeck>
{
    public void Configure(EntityTypeBuilder<TcgDeck> builder)
    {
        builder.HasKey(deck => deck.Id);

        builder
            .HasOne(deck => deck.Owner)
            .WithMany()
            .HasForeignKey(deck => deck.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deck names are unique per owner so they can be referenced by name in commands.
        builder
            .HasIndex(deck => new { deck.OwnerId, deck.Name })
            .IsUnique();
    }
}
