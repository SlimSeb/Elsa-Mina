using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class FloodItScoreConfiguration : IEntityTypeConfiguration<FloodItScore>
{
    public void Configure(EntityTypeBuilder<FloodItScore> builder)
    {
        builder.HasKey(score => score.UserId);

        builder
            .HasOne(score => score.User)
            .WithOne(user => user.FloodItScore)
            .HasForeignKey<FloodItScore>(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
