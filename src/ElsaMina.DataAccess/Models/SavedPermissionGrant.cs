using System.ComponentModel.DataAnnotations.Schema;

namespace ElsaMina.DataAccess.Models;

[Table("PermissionGrants")]
public class SavedPermissionGrant
{
    public string RoomId { get; set; }
    public string UserId { get; set; }
    public string Kind { get; set; }
    public string Target { get; set; }
    public string GrantedBy { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
