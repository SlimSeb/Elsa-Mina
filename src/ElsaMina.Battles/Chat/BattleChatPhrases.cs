namespace ElsaMina.Battles.Chat;

/// <summary>
/// The phrases the bot picks from in a battle's chat. None may start with "/" or "!", which Showdown reads as a
/// command.
/// </summary>
public static class BattleChatPhrases
{
    public static readonly IReadOnlyList<string> Start =
    [
        "Battle started. Good luck!",
        "Good luck, have fun!",
        "glhf!",
        "May the best trainer win!",
        "Let's have a good battle!",
        "Here we go. Good luck!",
        "Hi! Good luck and have fun.",
        "Let the battle begin!",
        "Good luck! I'll try not to get haxed.",
        "Ready when you are. Have fun!"
    ];

    public static readonly IReadOnlyList<string> Won =
    [
        "GG! I won.",
        "GG, that was fun!",
        "GG! Thanks for the battle.",
        "GG wp!",
        "Victory! GG, thanks for playing.",
        "GG! That one went my way.",
        "GG! Rematch anytime."
    ];

    public static readonly IReadOnlyList<string> Lost =
    [
        "GG! I lost.",
        "GG, well played!",
        "GG! You got me this time.",
        "GG wp, I'll get you next time.",
        "GG! Nicely done.",
        "GG! Back to the drawing board for me.",
        "GG! You outplayed me."
    ];

    public static readonly IReadOnlyList<string> Tied =
    [
        "Battle ended in a tie. GG!",
        "A tie? GG!",
        "GG! Nobody wins this one.",
        "GG! A draw, how rare."
    ];

    /// <summary>
    /// When the server did not name the winner.
    /// </summary>
    public static readonly IReadOnlyList<string> Ended =
    [
        "Battle ended. GG!",
        "GG!",
        "GG, thanks for the battle!"
    ];

    public static readonly IReadOnlyDictionary<BattleChatEvent, IReadOnlyList<string>> Reactions =
        new Dictionary<BattleChatEvent, IReadOnlyList<string>>
        {
            [BattleChatEvent.OwnCriticalHit] =
            [
                "Critical hit!",
                "Oh, a crit! Sorry about that.",
                "That crit was not planned, I promise.",
                "Crit! Lucky me.",
                "Sorry, the RNG likes me today."
            ],
            [BattleChatEvent.OpponentCriticalHit] =
            [
                "Ouch, a crit...",
                "A crit? Really?",
                "That crit hurt.",
                "Hax!",
                "Of course it crits."
            ],
            [BattleChatEvent.OwnMiss] =
            [
                "Missed?!",
                "Come on, that should have hit.",
                "My move missed... great.",
                "Accuracy is a lie.",
                "Miss. Classic."
            ],
            [BattleChatEvent.OpponentMiss] =
            [
                "Phew, it missed!",
                "Missed! Lucky me.",
                "Dodged it!",
                "Sorry about that miss."
            ],
            [BattleChatEvent.OwnFullParalysis] =
            [
                "Fully paralyzed... of course.",
                "Can't move! Ugh.",
                "Para strikes again.",
                "Come on, move!"
            ],
            [BattleChatEvent.OpponentFullParalysis] =
            [
                "Fully paralyzed! Sorry.",
                "Para doing its job.",
                "Can't move? Lucky me."
            ],
            [BattleChatEvent.OwnFlinch] =
            [
                "Flinched... ugh.",
                "A flinch? Seriously?",
                "No flinching, please."
            ],
            [BattleChatEvent.OpponentFlinch] =
            [
                "Flinch! Sorry about that.",
                "Flinched! Lucky.",
                "That flinch was free."
            ],
            [BattleChatEvent.OwnConfusionHit] =
            [
                "It hurt itself in confusion...",
                "Stop hitting yourself!",
                "Confusion, my old enemy."
            ],
            [BattleChatEvent.OpponentConfusionHit] =
            [
                "Hit itself in confusion!",
                "Confusion is on my side today."
            ],
            [BattleChatEvent.OwnParalysis] =
            [
                "Paralyzed... this will be fun.",
                "Para, great.",
                "Not the paralysis..."
            ],
            [BattleChatEvent.OpponentParalysis] =
            [
                "Paralyzed! Good luck moving.",
                "Para'd! Nice."
            ],
            [BattleChatEvent.OwnBurn] =
            [
                "Burned! Ouch.",
                "That burn hurts.",
                "Not a burn..."
            ],
            [BattleChatEvent.OpponentBurn] =
            [
                "Burned! Sorry about that.",
                "A burn! Nice."
            ],
            [BattleChatEvent.OwnFreeze] =
            [
                "Frozen?! Really?",
                "Frozen solid... this is fine.",
                "A freeze. Of course."
            ],
            [BattleChatEvent.OpponentFreeze] =
            [
                "Frozen! Sorry, that's hax.",
                "A freeze! Lucky me."
            ],
            [BattleChatEvent.OwnFaint] =
            [
                "Oh no, it fainted!",
                "You'll be avenged.",
                "Rest well, buddy.",
                "Down one."
            ],
            [BattleChatEvent.OpponentFaint] =
            [
                "Got it!",
                "One down!",
                "KO!",
                "Nice, that's a KO."
            ]
        };
}
