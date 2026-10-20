using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Permissions;

public class GrantListViewModel : LocalizableViewModel
{
    public required string RoomName { get; init; }
    public required IReadOnlyList<GrantListRow> Rows { get; init; }
}
