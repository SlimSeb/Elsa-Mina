namespace ElsaMina.Battles.Tracking;

/// <summary>
/// Raised where poke-env raises while parsing a line (unknown species, inconsistent message...). Like poke-env,
/// the tracker stops processing that line and keeps what it already changed.
/// </summary>
public class BattleTrackingException : Exception
{
    public BattleTrackingException(string message) : base(message)
    {
    }
}
