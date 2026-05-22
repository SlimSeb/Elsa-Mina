namespace ElsaMina.Commands.Games.TwentyFortyEight;

public static class TwentyFortyEightConstants
{
    public const int GRID_SIZE = 4;
    public const int TARGET_TILE = 2048;
    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(5);

    public static readonly IReadOnlyDictionary<int, (string Background, string Text)> TILE_STYLES = new Dictionary<int, (string Background, string Text)>()
    {
        [0]    = ("#cdc1b4", "#776e65"),
        [2]    = ("#eee4da", "#776e65"),
        [4]    = ("#ede0c8", "#776e65"),
        [8]    = ("#f2b179", "#f9f6f2"),
        [16]   = ("#f59563", "#f9f6f2"),
        [32]   = ("#f67c5f", "#f9f6f2"),
        [64]   = ("#f65e3b", "#f9f6f2"),
        [128]  = ("#edcf72", "#f9f6f2"),
        [256]  = ("#edcc61", "#f9f6f2"),
        [512]  = ("#edc850", "#f9f6f2"),
        [1024] = ("#edc53f", "#f9f6f2"),
        [2048] = ("#edc22e", "#f9f6f2"),
    };

    public static (string Background, string Text) GetTileStyle(int value)
    {
        if (TILE_STYLES.TryGetValue(value, out var style)) return style;
        return value == 0 ? ("#cdc1b4", "#776e65") : ("#3c3a32", "#f9f6f2");
    }
}
