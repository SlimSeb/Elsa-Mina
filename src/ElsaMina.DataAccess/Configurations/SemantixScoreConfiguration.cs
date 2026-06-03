using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class SemantixScoreConfiguration : IEntityTypeConfiguration<SemantixScore>
{
    public void Configure(EntityTypeBuilder<SemantixScore> builder)
    {
        builder.HasKey(score => score.UserId);

        builder
            .HasOne<SavedUser>()
            .WithMany()
            .HasForeignKey(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
