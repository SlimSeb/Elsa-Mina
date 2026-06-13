using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class ConnectFourRatingConfiguration : IEntityTypeConfiguration<ConnectFourRating>
{
    public void Configure(EntityTypeBuilder<ConnectFourRating> builder)
    {
        builder.HasKey(rating => rating.UserId);

        builder
            .HasOne(rating => rating.User)
            .WithOne(user => user.ConnectFourRating)
            .HasForeignKey<ConnectFourRating>(rating => rating.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
