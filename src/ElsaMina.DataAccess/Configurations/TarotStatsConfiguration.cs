using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class TarotStatsConfiguration : IEntityTypeConfiguration<TarotStats>
{
    public void Configure(EntityTypeBuilder<TarotStats> builder)
    {
        builder.HasKey(stats => stats.UserId);
    }
}
