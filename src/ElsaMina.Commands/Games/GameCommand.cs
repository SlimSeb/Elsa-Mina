using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games;

/// <summary>
/// Base class for commands that change a game's in-memory state. They run in order with the other messages of
/// their room (see <see cref="ICommand.RunsInMessageOrder"/>), so two moves, or a move and a chat answer read by a
/// game handler, never interleave.
/// </summary>
public abstract class GameCommand : Command
{
    public override bool RunsInMessageOrder => true;
}
