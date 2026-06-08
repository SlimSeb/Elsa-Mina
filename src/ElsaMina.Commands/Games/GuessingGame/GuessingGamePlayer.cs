namespace ElsaMina.Commands.Games.GuessingGame;

public sealed record GuessingGamePlayer(string UserId, string UserName)
{
    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }

    public bool Equals(GuessingGamePlayer other)
    {
        return Equals(UserId, other?.UserId);
    }
}