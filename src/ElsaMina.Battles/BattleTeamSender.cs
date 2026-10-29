using ElsaMina.Core;

namespace ElsaMina.Battles;

public class BattleTeamSender : IBattleTeamSender
{
    private const string NO_TEAM = "null";

    private readonly IBot _bot;
    private readonly Lock _lock = new();

    public BattleTeamSender(IBot bot)
    {
        _bot = bot;
    }

    public void SendWithTeam(string packedTeam, string command)
    {
        lock (_lock)
        {
            _bot.Send($"|/utm {(string.IsNullOrEmpty(packedTeam) ? NO_TEAM : packedTeam)}");
            _bot.Say(string.Empty, command);
        }
    }
}
