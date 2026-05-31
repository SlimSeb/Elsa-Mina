using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class MoneyConfiguration : IEntityTypeConfiguration<Money>
{
    public void Configure(EntityTypeBuilder<Money> builder)
    {
        builder.HasKey(money => money.Id);
    }
}
