using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class BattleshipRatingConfiguration : IEntityTypeConfiguration<BattleshipRating>
{
    public void Configure(EntityTypeBuilder<BattleshipRating> builder)
    {
        builder.HasKey(rating => rating.UserId);

        builder
            .HasOne(rating => rating.User)
            .WithOne(user => user.BattleshipRating)
            .HasForeignKey<BattleshipRating>(rating => rating.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
