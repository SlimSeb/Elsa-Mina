using System.Collections.Concurrent;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles;

public class BattleTeamsService : IBattleTeamsService
{
    private readonly ConcurrentDictionary<string, string> _packedTeamsByFormat = new();

    public void SetTeam(string format, string packedTeam)
    {
        _packedTeamsByFormat[format.ToLowerAlphaNum()] = packedTeam;
    }

    public string GetTeam(string format)
    {
        return _packedTeamsByFormat.GetValueOrDefault(format.ToLowerAlphaNum());
    }

    public bool RemoveTeam(string format)
    {
        return _packedTeamsByFormat.TryRemove(format.ToLowerAlphaNum(), out _);
    }
}
