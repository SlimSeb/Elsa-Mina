namespace ElsaMina.Commands.Games.FloodIt;

public static class FloodItConstants
{
    public const int MAX_LEVEL = 10;
    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(3);

    public static readonly string[] COLOR_HEX =
    [
        "#E74C3C", // Red
        "#E67E22", // Orange
        "#F1C40F", // Yellow
        "#2ECC71", // Green
        "#3498DB", // Blue
        "#9B59B6"  // Purple
    ];

    // (GridSize, ColorCount, TargetMoves, MaxMoves) per level
    // TargetMoves = optimal threshold (3 stars); MaxMoves = hard limit (loss if exceeded)
    // Leeway is 1 throughout - near-optimal play required to survive
    public static readonly (int GridSize, int ColorCount, int TargetMoves, int MaxMoves)[] LEVEL_CONFIGURATIONS =
    [
        (8,  6, 12, 13), // Level 1
        (10, 6, 16, 17), // Level 2
        (12, 6, 20, 21), // Level 3
        (12, 6, 23, 24), // Level 4
        (14, 6, 26, 27), // Level 5
        (14, 6, 29, 30), // Level 6
        (16, 6, 31, 32), // Level 7
        (16, 6, 34, 35), // Level 8
        (16, 6, 37, 38), // Level 9
        (16, 6, 40, 41), // Level 10
    ];

    public static (int GridSize, int ColorCount, int TargetMoves, int MaxMoves) GetLevelConfig(int level)
    {
        var index = Math.Clamp(level - 1, 0, LEVEL_CONFIGURATIONS.Length - 1);
        return LEVEL_CONFIGURATIONS[index];
    }
}
