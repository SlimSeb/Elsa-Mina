namespace ElsaMina.Battles;

public interface IBattleTeamsService
{
    event EventHandler<string> TeamChanged;
    void SetTeam(string format, string packedTeam);
    string GetTeam(string format);
    bool RemoveTeam(string format);
}
