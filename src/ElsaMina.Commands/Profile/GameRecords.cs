using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Profile;

public class GameRecords
{
    public FloodItScore FloodIt { get; init; }
    public LightsOutScore LightsOut { get; init; }
    public VoltorbFlipLevel VoltorbFlip { get; init; }
    public TwentyFortyEightScore TwentyFortyEight { get; init; }
    public ConnectFourRating ConnectFour { get; init; }
    public BattleshipRating Battleship { get; init; }
    public ChessRating Chess { get; init; }
    public WordleScore Wordle { get; init; }
    public SemantixScore Semantix { get; init; }
    public BeloteStats Belote { get; init; }
    public TarotStats Tarot { get; init; }

    public bool HasAnyRecord => FloodIt != null
                                || LightsOut != null
                                || VoltorbFlip != null
                                || TwentyFortyEight != null
                                || ConnectFour != null
                                || Battleship != null
                                || Chess != null
                                || Wordle != null
                                || Semantix != null
                                || Belote != null
                                || Tarot != null;
}
