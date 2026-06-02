using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class UserPointsConfiguration : IEntityTypeConfiguration<UserPoints>
{
    public void Configure(EntityTypeBuilder<UserPoints> builder)
    {
        builder.HasKey(points => points.Id);

        builder
            .HasOne<SavedUser>()
            .WithMany()
            .HasForeignKey(points => points.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
