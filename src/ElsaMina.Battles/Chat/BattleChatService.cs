using ElsaMina.Battles.Results;
using ElsaMina.Core.Services.Probabilities;

namespace ElsaMina.Battles.Chat;

public class BattleChatService : IBattleChatService
{
    // About one event in seven gets a reaction, so the chat stays a chat and not a commentary
    private const double REACTION_CHANCE = 1.0 / 7;
    private const string CONFUSION_SOURCE = "[from] confusion";
    // A status from an item is chosen (Flame Orb, Toxic Orb), not hax
    private const string ITEM_SOURCE_PREFIX = "[from] item:";

    private readonly IRandomService _randomService;

    public BattleChatService(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public string GetStartMessage()
    {
        return _randomService.RandomElement(BattleChatPhrases.Start);
    }

    public string GetEndMessage(BattleOutcome? outcome)
    {
        var phrases = outcome switch
        {
            BattleOutcome.Won => BattleChatPhrases.Won,
            BattleOutcome.Lost => BattleChatPhrases.Lost,
            BattleOutcome.Tied => BattleChatPhrases.Tied,
            _ => BattleChatPhrases.Ended
        };
        return _randomService.RandomElement(phrases);
    }

    public string GetReaction(string[] parts, BattleContext context)
    {
        if (parts.Length < 2)
        {
            return null;
        }

        if (parts[1] == "turn")
        {
            context.HasReactedThisTurn = false;
            return null;
        }

        // A watched battle has no side of ours to cheer for
        if (string.IsNullOrEmpty(context.SideId) || context.HasReactedThisTurn)
        {
            return null;
        }

        var chatEvent = DetectEvent(parts, context.SideId);
        if (chatEvent == null || _randomService.NextDouble() >= REACTION_CHANCE)
        {
            return null;
        }

        context.HasReactedThisTurn = true;
        return _randomService.RandomElement(BattleChatPhrases.Reactions[chatEvent.Value]);
    }

    private static BattleChatEvent? DetectEvent(string[] parts, string ownSideId)
    {
        if (parts.Length < 3)
        {
            return null;
        }

        // The pokemon the line names: "p1a: Garchomp" is on side p1
        var isOwnPokemon = parts[2].StartsWith(ownSideId, StringComparison.Ordinal);
        return parts[1] switch
        {
            // |-crit|TARGET: the attacker is on the other side
            "-crit" => Pick(isOwnPokemon, BattleChatEvent.OpponentCriticalHit, BattleChatEvent.OwnCriticalHit),
            // |-miss|SOURCE|TARGET
            "-miss" => Pick(isOwnPokemon, BattleChatEvent.OwnMiss, BattleChatEvent.OpponentMiss),
            // |cant|POKEMON|REASON
            "cant" when parts.Length >= 4 => DetectCantEvent(parts[3], isOwnPokemon),
            // |-damage|POKEMON|HP|[from] confusion
            "-damage" when parts.Length >= 5 && parts[4] == CONFUSION_SOURCE =>
                Pick(isOwnPokemon, BattleChatEvent.OwnConfusionHit, BattleChatEvent.OpponentConfusionHit),
            // |-status|POKEMON|STATUS, with a [from] when an ability or an item caused it
            "-status" when parts.Length >= 4 && !IsFromItem(parts) => DetectStatusEvent(parts[3], isOwnPokemon),
            "faint" => Pick(isOwnPokemon, BattleChatEvent.OwnFaint, BattleChatEvent.OpponentFaint),
            _ => null
        };
    }

    private static BattleChatEvent? DetectCantEvent(string reason, bool isOwnPokemon)
    {
        return reason switch
        {
            "par" => Pick(isOwnPokemon, BattleChatEvent.OwnFullParalysis, BattleChatEvent.OpponentFullParalysis),
            "flinch" => Pick(isOwnPokemon, BattleChatEvent.OwnFlinch, BattleChatEvent.OpponentFlinch),
            _ => null
        };
    }

    private static BattleChatEvent? DetectStatusEvent(string status, bool isOwnPokemon)
    {
        return status switch
        {
            "par" => Pick(isOwnPokemon, BattleChatEvent.OwnParalysis, BattleChatEvent.OpponentParalysis),
            "brn" => Pick(isOwnPokemon, BattleChatEvent.OwnBurn, BattleChatEvent.OpponentBurn),
            "frz" => Pick(isOwnPokemon, BattleChatEvent.OwnFreeze, BattleChatEvent.OpponentFreeze),
            _ => null
        };
    }

    private static bool IsFromItem(string[] parts)
    {
        return parts.Length >= 5 && parts[4].StartsWith(ITEM_SOURCE_PREFIX, StringComparison.Ordinal);
    }

    private static BattleChatEvent Pick(bool isOwnPokemon, BattleChatEvent ownEvent, BattleChatEvent opponentEvent)
    {
        return isOwnPokemon ? ownEvent : opponentEvent;
    }
}
