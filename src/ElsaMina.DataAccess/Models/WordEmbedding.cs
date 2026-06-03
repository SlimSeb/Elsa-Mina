using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("WordEmbeddings")]
public class WordEmbedding
{
    public string Word { get; set; }
    public string Model { get; set; }
    public byte[] Vector { get; set; }
}
