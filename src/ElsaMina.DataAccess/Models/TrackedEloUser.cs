using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("TrackedEloUsers")]
public class TrackedEloUser
{
    public string Format { get; set; }
    public string UserId { get; set; }
}
