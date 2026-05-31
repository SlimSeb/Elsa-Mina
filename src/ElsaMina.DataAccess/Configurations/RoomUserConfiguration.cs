using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class RoomUserConfiguration : IEntityTypeConfiguration<RoomUser>
{
    public void Configure(EntityTypeBuilder<RoomUser> builder)
    {
        builder
            .HasKey(userData => new { userData.Id, userData.RoomId });

        builder
            .Property(userData => userData.Money)
            .HasDefaultValue(100L);

        builder
            .HasOne(roomUser => roomUser.User)
            .WithMany(savedUser => savedUser.RoomData)
            .HasForeignKey(roomUser => roomUser.Id);
    }
}