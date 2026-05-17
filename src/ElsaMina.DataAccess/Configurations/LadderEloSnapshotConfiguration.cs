using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class LadderEloSnapshotConfiguration : IEntityTypeConfiguration<LadderEloSnapshot>
{
    public void Configure(EntityTypeBuilder<LadderEloSnapshot> builder)
    {
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedOnAdd();
        builder.HasIndex(snapshot => new { snapshot.UserId, snapshot.Format });
        builder.HasIndex(snapshot => snapshot.RecordedAt);
    }
}
