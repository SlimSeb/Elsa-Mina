namespace ElsaMina.Commands.Games.TwentyFortyEight;

public static class TwentyFortyEightConstants
{
    public const int GRID_SIZE = 4;
    public const int TARGET_TILE = 2048;
    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(5);

    private const string TEXT_DARK = "#776e65";
    private const string TEXT_LIGHT = "#f9f6f2";

    public static readonly IReadOnlyDictionary<int, (string Background, string Text)> TILE_STYLES = new Dictionary<int, (string Background, string Text)>()
    {
        [0]    = ("#cdc1b4", TEXT_DARK),
        [2]    = ("#eee4da", TEXT_DARK),
        [4]    = ("#ede0c8", TEXT_DARK),
        [8]    = ("#f2b179", TEXT_LIGHT),
        [16]   = ("#f59563", TEXT_LIGHT),
        [32]   = ("#f67c5f", TEXT_LIGHT),
        [64]   = ("#f65e3b", TEXT_LIGHT),
        [128]  = ("#edcf72", TEXT_LIGHT),
        [256]  = ("#edcc61", TEXT_LIGHT),
        [512]  = ("#edc850", TEXT_LIGHT),
        [1024] = ("#edc53f", TEXT_LIGHT),
        [2048] = ("#edc22e", TEXT_LIGHT),
    };

    public static (string Background, string Text) GetTileStyle(int value)
    {
        if (TILE_STYLES.TryGetValue(value, out var style)) return style;
        return value == 0 ? ("#cdc1b4", TEXT_DARK) : ("#3c3a32", TEXT_LIGHT);
    }
}
