using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class SavedPermissionGrantConfiguration : IEntityTypeConfiguration<SavedPermissionGrant>
{
    public void Configure(EntityTypeBuilder<SavedPermissionGrant> builder)
    {
        builder.HasKey(grant => new { grant.RoomId, grant.UserId, grant.Kind, grant.Target });
    }
}
