namespace ElsaMina.DataAccess.Models;

/// <summary>
/// A doll owned by a user in a room. The doll itself is defined by the Google Drive catalogue,
/// so only its id is stored here.
/// </summary>
public class DollHolding
{
    public string DollId { get; set; }

    public string RoomId { get; set; }
    public string UserId { get; set; }
    public virtual RoomUser RoomUser { get; set; }
}
