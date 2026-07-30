using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class DollHoldingConfiguration : IEntityTypeConfiguration<DollHolding>
{
    public void Configure(EntityTypeBuilder<DollHolding> builder)
    {
        builder
            .HasKey(dollHolding => new { dollHolding.DollId, dollHolding.UserId, dollHolding.RoomId });

        builder
            .HasOne(dollHolding => dollHolding.RoomUser)
            .WithMany(roomUser => roomUser.Dolls)
            .HasForeignKey(dollHolding => new { dollHolding.UserId, dollHolding.RoomId });
    }
}
