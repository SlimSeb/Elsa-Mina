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
            .HasOne<SavedUser>()
            .WithMany()
            .HasForeignKey(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
