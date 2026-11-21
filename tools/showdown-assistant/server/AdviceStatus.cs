namespace ElsaMina.BattleAssistant;

/// <summary>
/// What a <see cref="BattleAdvice"/> says about the room.
/// </summary>
public enum AdviceStatus
{
    /// <summary>Nothing new: the lines asked for no decision. The last advice still holds.</summary>
    Unchanged,

    /// <summary>The player must choose: <see cref="BattleAdvice.Actions"/> scores every legal choice.</summary>
    Advice,

    /// <summary>The player has nothing to choose (the opponent is switching after a faint...).</summary>
    Waiting,

    /// <summary>A choice the model does not make (team preview, doubles...): see <see cref="BattleAdvice.Message"/>.</summary>
    Unsupported,

    /// <summary>The battle ended.</summary>
    Over,

    /// <summary>The player left the room: the server forgot it.</summary>
    Closed
}
