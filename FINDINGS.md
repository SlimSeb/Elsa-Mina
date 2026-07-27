# Findings

Things noticed while de-duplicating the card games (Tarot, Belote, Président, Poker) that were left
alone because they are outside the scope of that refactor. None of them is a regression introduced by
it; each is pre-existing behaviour worth a decision.

## 1. Belote narrates trick wins into the room instead of the game log

`BeloteGame.ResolveTrickAsync` announces every trick with
`Context.ReplyLocalizedMessage("belote_trick_won", ...)`, so a deal posts eight chat messages into the
room. Tarot and Président append the same event to their in-panel log instead, which keeps the room
quiet and the narration next to the table.

Belote now inherits the log panel from `SubstitutableCardGame` and never uses it, so switching would
be a one-line change (`LogEvent("belote_trick_won", ...)`) plus a `BeloteLog.cshtml` template and the
`belote_trick_won` key already exists. Left alone because it changes what the room sees.

## 2. `forceResend` meant the opposite thing in Belote

Before the refactor, `BeloteGame.RenderPublicAsync(bool forceResend)` computed
`isChanging: forceResend || _publicPanelInitialized`, while Tarot and Président computed
`isChanging: _publicPanelInitialized && !forceResend`. Passing `forceResend: true` in Belote therefore
forced an in-place *update*, which is the opposite of what the name says and of what the other games
do.

Its only caller was `StartAsync`, where `_publicPanelInitialized` is always true (a game cannot start
with zero players, and every join renders the lobby panel), so the two formulas agreed on every
reachable path. The shared base now uses the Tarot formula and Belote's `StartDealAsync` simply calls
`RenderPublicAsync()`, which produces the identical panel write. Flagged in case the intent really was
to re-post at the bottom of the chat, in which case Belote is missing a `WipePublicPanel()` call there.

## 3. `TarotGame.ClosePlayerPages` was unreachable

Tarot defined `ClosePlayerPages()` and never called it: a cancelled tarot game leaves every player's
hand page open, showing a stale hand. Belote does call the equivalent from its `EndGame()`. The method
now lives on the shared base, so wiring it into `CancelAsync` would be a one-line change, but doing so
would alter what players see when a game is called off.

## 4. Only Poker checks who starts the game

`PokerGame` refuses `StartAsync` from a user who is not seated (`poker_start_not_a_player`). Tarot,
Belote and Président let anyone in the room close the lobby and deal, including a spectator. This is
pinned by tests as current behaviour on both sides; whether the three French games should adopt
Poker's guard is a product decision.

## 5. The game id counter is per closed generic type

`SeatedCardGame<TPlayer>` holds `private static int _nextGameId`. Because the class is generic, each
closed type (`SeatedCardGame<TarotPlayer>`, `SeatedCardGame<BelotePlayer>`, ...) gets its own counter,
which is exactly what the four separate counters did before. Hoisting the field into a non-generic
base or making it shared would silently change every panel id. Worth remembering before "simplifying"
it.
