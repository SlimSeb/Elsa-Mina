using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("ChannelAlerts")]
public class ChannelAlert
{
    public string RoomId { get; set; }
    public string Platform { get; set; }
    public string ChannelId { get; set; }
    public string ChannelName { get; set; }
}
