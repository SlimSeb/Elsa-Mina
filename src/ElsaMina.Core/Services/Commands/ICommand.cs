using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Services.Commands;

public interface ICommand
{
    public string Name { get; }
    public IEnumerable<string> Aliases { get; }
    public string Category { get; }
    public bool IsAllowedInPrivateMessage { get; }
    public bool IsWhitelistOnly { get; }
    public bool IsPrivateMessageOnly { get; }
    public Rank RequiredRank { get; }
    public string HelpMessageKey { get; }
    public bool IsHidden { get; }
    public IEnumerable<string> RoomRestriction { get; }

    /// <summary>
    /// When true, the command runs in order with the other messages of its room: the room's next message is only
    /// handled once the command has finished. Use it for commands that change in-memory state shared with other
    /// messages, such as a game. Never use it for a command that waits for a message from the server, it would
    /// wait for a message queued behind itself. When false (the default), the command runs in the background.
    /// </summary>
    public bool RunsInMessageOrder { get; }

    Task RunAsync(IContext context, CancellationToken cancellationToken = default);
}