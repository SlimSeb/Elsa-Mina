using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("LadderEloSnapshots")]
public class LadderEloSnapshot
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string Format { get; set; }
    public int Elo { get; set; }
    public DateTime RecordedAt { get; set; }
}
