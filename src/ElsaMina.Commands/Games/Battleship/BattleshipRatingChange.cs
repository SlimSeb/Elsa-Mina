namespace ElsaMina.Commands.Games.Battleship;

public record BattleshipRatingChange(int OldRating, int NewRating)
{
    public int Delta => NewRating - OldRating;
}
