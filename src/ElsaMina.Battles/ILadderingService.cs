namespace ElsaMina.Battles;

public interface ILadderingService
{
    bool IsLaddering { get; }
    string Format { get; }
    LadderingStatus Status { get; }
    void Start(string format);
    void Stop();
    void OnBattleEnded();

    /// <summary>
    /// What the server's last updatesearch says: whether the bot is searching for a battle, and whether it plays one.
    /// </summary>
    void OnSearchStateUpdated(bool isSearching, bool isInGame);
}
