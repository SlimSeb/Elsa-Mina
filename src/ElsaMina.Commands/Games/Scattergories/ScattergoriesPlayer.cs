namespace ElsaMina.Commands.Games.Scattergories;

public sealed record ScattergoriesPlayer(string UserId, string UserName)
{
    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }

    public bool Equals(ScattergoriesPlayer other)
    {
        return Equals(UserId, other?.UserId);
    }
}
