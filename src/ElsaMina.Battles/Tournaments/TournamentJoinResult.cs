namespace ElsaMina.Battles.Tournaments;

public enum TournamentJoinResult
{
    /// <summary>
    /// /tour join was sent.
    /// </summary>
    Joining,
    NoTournament,
    AlreadyJoined,
    AlreadyStarted,
    UnsupportedFormat
}
