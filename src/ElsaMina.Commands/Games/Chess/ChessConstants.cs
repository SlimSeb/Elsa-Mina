namespace ElsaMina.Commands.Games.Chess;

public static class ChessConstants
{
    public const int BOARD_SIZE = 8;
    public const int MAX_PLAYERS_COUNT = 2;

    // Blitz time control: each player gets a 3 minute clock with a 2 second increment per move.
    public static readonly TimeSpan INITIAL_CLOCK = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan CLOCK_INCREMENT = TimeSpan.FromSeconds(2);

    // How often the live clock box refreshes while a game is running.
    public static readonly TimeSpan CLOCK_REFRESH_INTERVAL = TimeSpan.FromSeconds(1);
}
