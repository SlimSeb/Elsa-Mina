namespace ElsaMina.Commands.Permissions;

public class GrantListRow
{
    public required string UserId { get; init; }
    public required string Right { get; init; }
    public required string GrantedBy { get; init; }
    public required string Expires { get; init; }
}
