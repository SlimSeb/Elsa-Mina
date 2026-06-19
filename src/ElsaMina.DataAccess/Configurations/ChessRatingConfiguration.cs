using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class ChessRatingConfiguration : IEntityTypeConfiguration<ChessRating>
{
    public void Configure(EntityTypeBuilder<ChessRating> builder)
    {
        builder.HasKey(rating => rating.UserId);

        builder
            .HasOne(rating => rating.User)
            .WithOne(user => user.ChessRating)
            .HasForeignKey<ChessRating>(rating => rating.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
