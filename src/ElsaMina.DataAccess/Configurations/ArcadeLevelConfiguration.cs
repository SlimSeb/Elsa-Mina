using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class ArcadeLevelConfiguration : IEntityTypeConfiguration<ArcadeLevel>
{
    public void Configure(EntityTypeBuilder<ArcadeLevel> builder)
    {
        builder.HasKey(level => level.Id);

        builder
            .HasOne<SavedUser>()
            .WithMany()
            .HasForeignKey(level => level.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
