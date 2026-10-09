namespace ElsaMina.Commands.Games.VoltorbFlip;

public static class VoltorbFlipConstants
{
    public const int GRID_SIZE = 5;
    public const int MAX_LEVEL = 8;
    public const string VOLTORB_SPRITE_URL = "https://i.imgur.com/mrFUlA2.png";
    public static readonly TimeSpan INACTIVITY_TIMEOUT = TimeSpan.FromMinutes(1);

    // Each level has 5 possible board configurations: (twos, threes, voltorbs)
    public static readonly (int Twos, int Threes, int Voltorbs)[][] LEVEL_CONFIGURATIONS =
    [
        // Level 1
        [(3, 1, 6), (0, 3, 6), (5, 0, 6), (2, 2, 6), (4, 1, 6)],
        // Level 2
        [(1, 3, 7), (6, 0, 7), (3, 2, 7), (0, 4, 7), (5, 1, 7)],
        // Level 3
        [(2, 3, 8), (7, 0, 8), (4, 2, 8), (1, 4, 8), (6, 1, 8)],
        // Level 4
        [(3, 3, 8), (0, 5, 8), (8, 0, 10), (5, 2, 10), (2, 4, 10)],
        // Level 5
        [(7, 1, 10), (4, 3, 10), (1, 5, 10), (9, 0, 10), (6, 2, 10)],
        // Level 6
        [(3, 4, 10), (0, 6, 10), (8, 1, 10), (5, 3, 10), (2, 5, 10)],
        // Level 7
        [(7, 2, 10), (4, 4, 10), (1, 6, 13), (9, 1, 13), (6, 3, 10)],
        // Level 8
        [(0, 7, 10), (8, 2, 10), (5, 4, 10), (2, 6, 10), (7, 3, 10)]
    ];
}
