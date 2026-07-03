namespace ElsaMina.Battles;

public interface ILadderingService
{
    bool IsLaddering { get; }
    string Format { get; }
    void Start(string format);
    void Stop();
    void OnBattleEnded();
}
