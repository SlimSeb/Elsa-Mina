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
            .WithOne(user => user.WordleScore)
            .HasForeignKey<WordleScore>(score => score.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
