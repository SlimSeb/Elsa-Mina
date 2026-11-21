# Showdown battle assistant

A Firefox extension that shows, during your own Pokémon Showdown battles, what ElsaMina's latest minipoke model
(`minipokev3-22m`) would play: every legal choice with the model's probability, the model's evaluation of the
position, and an outline on the Showdown button it recommends.

```
play.pokemonshowdown.com                           your machine
┌───────────────────────────────────────┐          ┌────────────────────────────────────┐
│ hook.js        copies battle messages │  frames  │ ElsaMina.BattleAssistant (server/) │
│ background.js  calls the server       │  ─────►  │   BattleMessageParser              │
│ content.js     panel, button outline  │  ◄─────  │   MiniPokeV2Encoder                │
└───────────────────────────────────────┘  advice  │   minipoke.onnx                    │
                                                   │   MiniPokeActionGuard              │
                                                   │   MiniPokeTurnSearch (optional)    │
                                                   └────────────────────────────────────┘
```

The model doesn't run inside the extension. It runs in a small local server that references `ElsaMina.Battles` and
plays a battle's lines through the same code as the bot: the tracker, encoder, ONNX model, guard and search. The
parity tests that keep the bot faithful to the Python encoder therefore cover the assistant too. Porting the
tracker and encoder (about 4,000 lines) to JavaScript would have meant a second copy to keep in step, and a
mismatch there would not show: the model still suggests legal moves, it just plays worse.

## Run it

1. **Server** (needs the .NET 11 SDK):

   ```bash
   cd tools/showdown-assistant/server
   dotnet run
   ```

   It listens on `http://127.0.0.1:7685` and logs `minipokev3-22m loaded`. Options (they override
   `appsettings.json`):

   | Option | Default | |
   |---|---|---|
   | `--ShowdownDirectory <dir>` | empty | A pokemon-showdown package (its `dist/sim`, e.g. `node_modules/pokemon-showdown`). Turns on the one-turn search, which the bot measured as stronger than the model alone. Needs Node. |
   | `--NodePath <path>` | `node` | The Node executable the search runs. |
   | `--Model <name>` | `MiniPokeV3_22M` | Another model played with the V2 encoder: `MiniPokeV2_1_22M`. |
   | `--urls <url>` | `http://127.0.0.1:7685` | Where it listens. Change the extension's server address to match. |

2. **Extension** (Firefox 128 or later): open `about:debugging#/runtime/this-firefox`, click
   **Load Temporary Add-on…** and pick `tools/showdown-assistant/extension/manifest.json`. A temporary add-on stays
   until Firefox restarts. To keep it installed, sign it as an unlisted add-on (`web-ext sign`), or use Firefox
   Developer Edition or Nightly with `xpinstall.signatures.required` set to false.

3. Open (or reload) Showdown and start a **gen9randombattle**. Both clients work: the classic one
   (play.pokemonshowdown.com) and the new one (play.pokemonshowdown.com/preactalpha). If the toolbar popup shows
   **Allow access**, click it: the extension needs permission for Showdown and for the local server.

## What the panel shows

- **Eval**: the model's value of the position, from about -1 (losing) to +1 (winning).
- **One row per legal choice**, most likely first, with the model's probability. The highlighted row is the
  recommendation, and the matching Showdown button gets a blue outline. With tera, the Terastallize checkbox does
  too.
- **Struck-through rows**: choices the guard rules out because they are sure to be wasted (a move that already had
  no effect on this target, healing at full HP, a hazard already maxed, a switch while trapped). Hover a row to see
  why.
- **Search**: with `--ShowdownDirectory`, the server simulates one turn for the top candidates (about a second). When
  the search disagrees with the model, its pick becomes the recommendation, and both rows get a badge.

You can drag the panel by its header and collapse it. The popup turns advice and the search on or off, and sets the
server address.

## Limits

- The model learned gen9randombattle singles. In other singles formats the panel says its advice is a guess. It
  gives no advice at team preview or in doubles.
- The extension starts copying messages when the page loads. For a battle that was open before the extension was
  installed, reload the page: Showdown then sends the whole battle again. If the server restarts mid-battle, the
  extension sends it the battle from the start once it answers again.
- It reads the messages where each client receives them: the classic client's WebSocket (SockJS frames), and the
  new client's connection Worker (`/js/client-connection-worker.js`), or its WebSocket when the Worker fails. If
  SockJS falls back to another transport (rare, behind some proxies), no advice shows. The buttons are found from
  each client's markup (`chooseMove` / `chooseSwitch` buttons in the classic client, `data-cmd="/move 2"` and
  `data-cmd="/switch 3"` in the new one): a Showdown update that changes them stops the outline, not the panel.
- Play fair: getting move advice in rated battles against people is unfair to them. Use it to review your play,
  in unrated battles, or against friends who know about it.

## HTTP API

| Request | Body | Answer |
|---|---|---|
| `GET /status` | | `{ model, loaded, canSearch, trainedFormat }` |
| `POST /battles/{roomId}/frames` | `{ frames: [">battle-…\n|…"], reset }` | The advice after applying the frames in order: `status` (`unchanged`, `advice`, `waiting`, `unsupported`, `over`, `closed`), `rqid`, `turn`, `value`, `best`, and `actions` with each choice's `label`, `probability`, `veto` and `choice` (`move 2 terastallize`). `partial` means the server didn't see the battle's start. |
| `POST /battles/{roomId}/search` | `{ rqid }` | `{ status, action }`: the action the one-turn search picks for that request. |
| `DELETE /battles/{roomId}` | | Forgets the room. |

The server only listens on 127.0.0.1, and it only answers cross-origin calls from `moz-extension://` origins.

## Development

- The server is not in `ElsaMina.slnx`: the Docker image restores the solution with only `src/` and `test/` copied.
  Build it after changing `ElsaMina.Battles`: `dotnet build tools/showdown-assistant/server`.
- `tests/replay_fixtures.py` replays the parity fixtures through the HTTP API and compares the advice with what
  Python recorded. On the 40 MiniPokeV3_22M fixtures (1,303 decisions), the recommendation matches everywhere except
  the 5 decisions where the guard vetoes the model's pick.
- `tests/extension_harness.mjs` runs `hook.js`, `background.js` and `content.js` in Node against a running server,
  in a fake page of either client (`new`, the default, or `classic` as its last argument). It checks the panel, the
  button outline, choices, a server outage and the battle's end.
- Both need a running server. See the comment at the top of each script.
