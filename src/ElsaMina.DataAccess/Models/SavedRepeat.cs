using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("Repeats")]
public class SavedRepeat
{
    public Guid Id { get; set; }
    public string RoomId { get; set; }
    public string Message { get; set; }
    public TimeSpan Interval { get; set; }
}
