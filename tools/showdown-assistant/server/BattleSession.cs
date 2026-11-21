using ElsaMina.Battles;

namespace ElsaMina.BattleAssistant;

/// <summary>
/// One battle room the extension forwards: its <see cref="BattleContext"/>, the way BattleService keeps one per room.
/// </summary>
public sealed class BattleSession
{
    private bool _hasStarted;

    public BattleSession(string roomId)
    {
        RoomId = roomId;
        Context = new BattleContext(roomId);
    }

    public string RoomId { get; }

    /// <summary>Held while lines are applied or read: one request at a time per room.</summary>
    public Lock Gate { get; } = new();

    public BattleContext Context { get; private set; }

    /// <summary>The decision the room waits for, null when there is none.</summary>
    public PendingDecision? Pending { get; set; }

    /// <summary>Whether the room's first line was not its |init|battle (the server joined mid-battle).</summary>
    public bool IsPartial { get; private set; }

    /// <summary>When a request last touched the room (<see cref="Environment.TickCount64"/>).</summary>
    public long LastUsed { get; private set; } = Environment.TickCount64;

    /// <summary>
    /// The room starts over: its |init|battle came (a new battle, or Showdown sending the log again on reconnect).
    /// </summary>
    public void Reset()
    {
        Context = new BattleContext(RoomId);
        Pending = null;
        _hasStarted = true;
        IsPartial = false;
    }

    /// <summary>
    /// A line other than |init|battle came: the first one makes the room partial.
    /// </summary>
    public void MarkStarted()
    {
        if (!_hasStarted)
        {
            _hasStarted = true;
            IsPartial = true;
        }
    }

    public void Touch() => LastUsed = Environment.TickCount64;
}
