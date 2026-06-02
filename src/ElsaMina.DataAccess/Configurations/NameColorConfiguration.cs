using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class NameColorConfiguration : IEntityTypeConfiguration<NameColor>
{
    public void Configure(EntityTypeBuilder<NameColor> builder)
    {
        builder.HasKey(nameColor => nameColor.UserId);

        builder
            .HasOne<SavedUser>()
            .WithMany()
            .HasForeignKey(nameColor => nameColor.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
