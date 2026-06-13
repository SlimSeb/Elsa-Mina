using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class TwentyFortyEightScoreConfiguration : IEntityTypeConfiguration<TwentyFortyEightScore>
{
    public void Configure(EntityTypeBuilder<TwentyFortyEightScore> builder)
    {
        builder.HasKey(score => score.UserId);

        builder
            .HasOne(score => score.User)
            .WithOne(user => user.TwentyFortyEightScore)
            .HasForeignKey<TwentyFortyEightScore>(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
