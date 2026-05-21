using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class TrackedEloUserConfiguration : IEntityTypeConfiguration<TrackedEloUser>
{
    public void Configure(EntityTypeBuilder<TrackedEloUser> builder)
    {
        builder.HasKey(entry => new { entry.Format, entry.UserId });
    }
}
