using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class LightsOutScoreConfiguration : IEntityTypeConfiguration<LightsOutScore>
{
    public void Configure(EntityTypeBuilder<LightsOutScore> builder)
    {
        builder.HasKey(score => score.UserId);

        builder
            .HasOne<SavedUser>()
            .WithMany()
            .HasForeignKey(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
