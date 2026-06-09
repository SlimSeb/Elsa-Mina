using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class BeloteStatsConfiguration : IEntityTypeConfiguration<BeloteStats>
{
    public void Configure(EntityTypeBuilder<BeloteStats> builder)
    {
        builder.HasKey(stats => stats.UserId);

        builder
            .HasOne<SavedUser>()
            .WithMany()
            .HasForeignKey(stats => stats.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
