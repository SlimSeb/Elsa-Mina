using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Gui.Pages.Room;

public class RoomUserViewModel
{
    public string Name { get; }
    public bool IsIdle { get; }
    public Rank Rank { get; }

    public string RankSymbol => Rank switch
    {
        Rank.Admin => "~",
        Rank.Leader => "&",
        Rank.RoomOwner => "#",
        Rank.Mod => "@",
        Rank.Driver => "%",
        Rank.Bot => "*",
        Rank.Voiced => "+",
        Rank.FormerStaff => "!",
        _ => " "
    };

    public double NameOpacity => IsIdle ? 0.45 : 1.0;

    public RoomUserViewModel(IUser user)
    {
        Name = user.Name;
        IsIdle = user.IsIdle;
        Rank = user.Rank;
    }
}
