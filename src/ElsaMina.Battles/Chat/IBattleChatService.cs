using ElsaMina.Battles.Results;

namespace ElsaMina.Battles.Chat;

/// <summary>
/// What the bot says in a battle's chat: a phrase picked at random when the battle starts and ends, and now and
/// then a reaction to what just happened (a critical hit, a miss, a paralysis...).
/// </summary>
public interface IBattleChatService
{
    string GetStartMessage();

    /// <param name="outcome">Null when the server did not name the winner.</param>
    string GetEndMessage(BattleOutcome? outcome);

    /// <summary>
    /// The reaction to a line of the battle's log, or null when the bot stays quiet: most of the time, always in
    /// a battle it only watches, and once it has reacted during the turn.
    /// </summary>
    string GetReaction(string[] parts, BattleContext context);
}
