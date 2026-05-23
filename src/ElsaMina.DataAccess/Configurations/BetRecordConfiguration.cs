using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class BetRecordConfiguration : IEntityTypeConfiguration<BetRecord>
{
    public void Configure(EntityTypeBuilder<BetRecord> builder)
    {
        builder
            .HasKey(record => new { record.UserId, record.RoomId });

        builder
            .HasOne(record => record.RoomUser)
            .WithOne(user => user.BetRecord)
            .HasForeignKey<BetRecord>(record => new { record.UserId, record.RoomId });
    }
}
