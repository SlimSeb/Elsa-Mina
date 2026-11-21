namespace ElsaMina.BattleAssistant;

/// <summary>
/// Messages Showdown sent to a battle room, in order, as received (">battle-...\n|line\n|line").
/// </summary>
/// <param name="Frames">The messages.</param>
/// <param name="Reset">Forget the room first: the frames replay it from its |init|battle.</param>
public sealed record FramesRequest(IReadOnlyList<string>? Frames, bool Reset = false);
