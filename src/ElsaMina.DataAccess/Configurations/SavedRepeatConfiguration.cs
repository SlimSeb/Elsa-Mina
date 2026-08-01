using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class SavedRepeatConfiguration : IEntityTypeConfiguration<SavedRepeat>
{
    public void Configure(EntityTypeBuilder<SavedRepeat> builder)
    {
        builder.HasKey(repeat => repeat.Id);
    }
}
