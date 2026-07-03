namespace ElsaMina.Battles;

public interface IBattleTeamsService
{
    void SetTeam(string format, string packedTeam);
    string GetTeam(string format);
    bool RemoveTeam(string format);
}
