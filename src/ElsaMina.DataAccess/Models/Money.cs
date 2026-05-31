using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("Money")]
public class Money
{
    public string Id { get; set; }
    public string RoomId { get; set; }
    public long Amount { get; set; }
}
