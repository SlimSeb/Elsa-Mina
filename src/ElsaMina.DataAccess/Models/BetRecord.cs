using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("BetRecords")]
public class BetRecord
{
    public string UserId { get; set; }
    public string RoomId { get; set; }
    public int CorrectBetsCount { get; set; }
    public int TotalBetsCount { get; set; }
    public RoomUser RoomUser { get; set; }
}
