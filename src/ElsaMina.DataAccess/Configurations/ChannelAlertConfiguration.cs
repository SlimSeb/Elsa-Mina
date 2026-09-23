using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class ChannelAlertConfiguration : IEntityTypeConfiguration<ChannelAlert>
{
    public void Configure(EntityTypeBuilder<ChannelAlert> builder)
    {
        builder.HasKey(alert => new { alert.RoomId, alert.Platform, alert.ChannelId });
    }
}
