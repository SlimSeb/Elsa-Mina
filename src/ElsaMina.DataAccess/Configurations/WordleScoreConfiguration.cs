using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class WordleScoreConfiguration : IEntityTypeConfiguration<WordleScore>
{
    public void Configure(EntityTypeBuilder<WordleScore> builder)
    {
        builder.HasKey(score => score.UserId);

        builder
            .HasOne(score => score.User)
            .WithMany()
            .HasForeignKey(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
