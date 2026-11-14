namespace ElsaMina.Battles.Chat;

/// <summary>
/// A moment of a battle the bot may react to in the battle's chat. An "Own" event happens to the bot's pokemon
/// (for a critical hit or a miss, the one that attacked), an "Opponent" event to the opponent's.
/// </summary>
public enum BattleChatEvent
{
    OwnCriticalHit,
    OpponentCriticalHit,
    OwnMiss,
    OpponentMiss,
    OwnFullParalysis,
    OpponentFullParalysis,
    OwnFlinch,
    OpponentFlinch,
    OwnConfusionHit,
    OpponentConfusionHit,
    OwnParalysis,
    OpponentParalysis,
    OwnBurn,
    OpponentBurn,
    OwnFreeze,
    OpponentFreeze,
    OwnFaint,
    OpponentFaint
}
