using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElsaMina.DataAccess.Configurations;

public class WordEmbeddingConfiguration : IEntityTypeConfiguration<WordEmbedding>
{
    public void Configure(EntityTypeBuilder<WordEmbedding> builder)
    {
        builder.HasKey(embedding => new { embedding.Word, embedding.Model });
    }
}
