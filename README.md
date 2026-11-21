# Elsa-Mina

[![deploy](https://github.com/SlimSeb/Elsa-Mina/actions/workflows/deploy.yml/badge.svg)](https://github.com/SlimSeb/Elsa-Mina/actions/workflows/deploy.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=slimf1_Elsa-Mina&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=slimf1_Elsa-Mina)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=slimf1_Elsa-Mina&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=slimf1_Elsa-Mina)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=slimf1_Elsa-Mina&metric=coverage)](https://sonarcloud.io/summary/new_code?id=slimf1_Elsa-Mina)

A chat bot for Pokémon Showdown, used in the French Room and private communities. It runs chat commands,
games, tournaments and room tooling, and it can play gen 9 battles on its own with small neural networks.

**[Live Demo](https://psim.us/fr)** • **[Report Bug](https://github.com/SlimSeb/Elsa-Mina/issues)**

## Prerequisites
- .NET 11.0 or higher
- PostgreSQL 12 or higher
- Docker (optional, for containerized deployment)
- Node.js and a built [pokemon-showdown](https://github.com/smogon/pokemon-showdown) checkout (optional, only needed
  for the battle bot's one-turn search, see [Battle System](#battle-system))

## Quick Start
1. Clone the repository
```bash
git clone https://github.com/SlimSeb/Elsa-Mina.git
cd Elsa-Mina
```

2. Configure the application
```bash
cp src/ElsaMina.Console/example.config.json src/ElsaMina.Console/config.json
# Edit config.json with your Showdown credentials and database connection.
```

3. Restore dependencies and build
```bash
./scripts/Build/restore.sh
./scripts/Build/build.sh
```

4. Run the bot
```bash
cd src/ElsaMina.Console
dotnet run
```

## Docker
You can also run the bot in a Docker container.
```bash
docker build -t elsa-mina .
docker run -d \
  --name elsa-mina \
  -v $(pwd)/src/ElsaMina.Console/config.json:/app/config.json \
  elsa-mina
```

## Configuration
Config lives at `src/ElsaMina.Console/config.json`. Copy `example.config.json` and fill in the relevant values.

- **Required**: `Host`, `Port`, `Name`, `Password`, `Rooms`, `DefaultRoom`, `ConnectionString`.
- **Behaviour**: `Trigger` (command prefix, `-` by default), `Whitelist` (bot administrators), `DefaultLocaleCode`.
- **Optional API keys**: each one turns on the features that need it (YouTube, Genius, Riot, Mistral/OpenAI,
  ElevenLabs, KLIPY, Unsplash, Spoonacular, Twitch, Twitter, S3, Google service account...). Features whose key is
  missing stay off.
- **Battle bot**: `ShowdownDirectory` and `NodePath` enable the one-turn search, `ReplaysDirectory` sets where
  `-savereplays` writes battles.
- **Observability**: `LokiUrl`, `LokiUser`, `LokiApiKey` and `OtlpEndpoint` send logs and metrics to Grafana.

## Database
We use PostgreSQL for persistence. The database connection is managed by Entity Framework Core.
To update the database schema, run the following commands in the root directory:

```bash
dotnet ef migrations add <MigrationName> --project src/ElsaMina.DataAccess
dotnet ef database update --project src/ElsaMina.DataAccess
dotnet ef migrations remove --project src/ElsaMina.DataAccess
```

## Project Layout
| Project | Role |
|---|---|
| `ElsaMina.Console` | Entry point: reads `config.json`, builds the Autofac container, runs the bot |
| `ElsaMina.Core` | Bot runtime: connection, per-room message dispatch, handlers, commands, rooms, templates, shared services |
| `ElsaMina.Commands` | Every chat feature: commands, handlers and their services, one folder per feature |
| `ElsaMina.Battles` | Autonomous battle bot: protocol parsing, battle tracking, decision strategies (minipoke models) |
| `ElsaMina.DataAccess` | EF Core models, migrations and repositories (PostgreSQL) |
| `ElsaMina.Cloud` | S3 uploads, Google Drive and Google Sheets |
| `ElsaMina.Logging` | Logging abstraction over Serilog |
| `tools/minipoke` | Python training code for the battle models (see [its README](tools/minipoke/README.md)) |

Dependencies between projects are checked by a unit test: Core knows no feature or infrastructure project, and
`Commands` and `Battles` never reference each other. See [CLAUDE.md](CLAUDE.md) and [CONTRIBUTING.md](CONTRIBUTING.md)
for the architecture and conventions.

## Testing
The code is unit-tested with NUnit and NSubstitute. Two test projects are available:
- `ElsaMina.UnitTests` for unit tests, including architecture rules, a container smoke test that resolves every
  command and handler, and parity tests that replay recorded battles through the minipoke encoders.
- `ElsaMina.IntegrationTests` for more complex integration tests.

To run the tests, run the following command in the root directory:
```bash
./scripts/Build/test.sh
```

Every project builds with the SonarCloud analyzers. `./scripts/Build/code-analysis.sh` fails on any issue
SonarCloud would report, like the `code-analysis` CI job.

## Scripts
The following scripts are available:
```bash
./scripts/Build/restore.sh       # Restore NuGet packages
./scripts/Build/build.sh         # Build the solution
./scripts/Build/test.sh          # Run all tests
./scripts/Build/code-analysis.sh # Fail on any analyzer issue SonarCloud reports
./scripts/Build/publish.sh       # Publish for deployment
./scripts/Build/full_publish.sh  # Do a full restore, test, build and publish
```

## Features

### Games
Every game can be listed with `-games`, and room staff can mute them during events (`-mutegames`).

- **Solo puzzles** (playable in private messages, with leaderboards):
  - **Voltorb Flip**: Pokémon HGSS' Voltorb Flip with marks
  - **Lights Out**, **Flood It** and **2048**
  - **Wordle**: daily word in English or French, with a private guess input on the board
  - **Semantix**: daily French word to find by meaning, scored by word embeddings
  - **Blackjack**: hit/stand against the dealer
- **Board games** (2 players):
  - **Chess**, **Connect Four** and **Battleship**, each with an Elo rating and a leaderboard
  - **Rock Paper Scissors**
- **Card games** (multiplayer):
  - **Tarot** and **Belote**, French trick-taking games with full scoring, stats and leaderboards
  - **President**
  - Tarot, Belote and President let a substitute take the seat of a player who leaves
  - **Poker**: Texas hold'em, with a buy-in in room bucks when the economy is on
- **Room-wide games**:
  - **Guessing games**: Pokémon descriptions, cries, Who's That Pokémon, higher or lower, gatekeepers, countries,
    capital cities, and trivia (Open Trivia DB)
  - **Scattergories**: name as many Pokémon as possible starting with a given letter
  - **Poke Race**: multi-player racing mini-game
  - **Slots**: slot machine mini-game

### Tournaments
- **Weekly Tournament Templates**: 23+ pre-configured formats (OU, RU, BH, AAA, 1v1, ZU, and more)
- **Random Tournaments**: Create random tournaments (single elimination and round robin)
- **Tournament Configuration**: Save, edit, and launch custom tournament setups
- **Tournament Leaderboard & History**: Top tournament players per room and a history of past tournaments
- **Tournament Betting**: Place bets on tournament participants, with winner payouts and a bettor leaderboard
- **Wins Transfer**: Move tournament wins from one account to another, directly or through a request
- **Sample Teams on Tours**: Show the room's sample teams for the format when a tournament is created

### Arcade Commands
- **Event Registration**: Join/leave arcade events, manage participants
- **Points & Leaderboard**: Track scores across arcade events
- **Achievement Tiers**: Configurable level tiers with display
- **Hall of Fame**: Persistent records synced to Google Sheets
- **CAA Integration**: Dedicated CAA (Advent calendar) event tracking
- **Discord Event Alerts**: Announce arcade events on Discord, pinging the role mapped to each event

### User Profiles & Social
- **Profiles**: Display user profile with avatar, title, colors, text, and badge showcase, editable from a panel
- **Badges & Trophies**: Create, award, and revoke badges with a management panel
- **Dolls**: Collectible images stored on Google Drive, given to users and shown on a reorderable shelf in their profile
- **Economy**: Per-room currency (bucks) earned in tournaments, with balance, transfers, and a leaderboard
- **Custom Name Colors**: Per-user HTML color for display names
- **Seen**: Track when a user was last active
- **Playtime Tracking**: Per-user playtime with top leaderboard
- **Join Phrases**: Personalized greeting when entering a room
- **Alts Detection**: Display known alternate accounts
- **Streaks**: Track consecutive daily activity streaks with a top leaderboard

### AI & Media
- **AI Chat**: Conversational AI with multi-turn history and selectable personalities (OpenAI / Google / Mistral)
- **AI Damage Calc**: Describe a matchup in plain words and get a Smogon-style damage calculation
- **Text-to-Speech**: Generate audio responses from text (ElevenLabs)
- **YouTube / Dailymotion Search**: Embed video search results
- **Lyrics Search**: Look up song lyrics via Genius
- **Wikipedia / Bulbapedia / Pokepedia**: Inline wiki searches
- **Random Images**: Animal and themed image commands (cat, dog, bird, capybara, and 20+ more) via Unsplash
- **KLIPY GIF Search**: Search and display GIF mosaics from KLIPY, with per-user and per-room cooldowns and a room-level enable/disable toggle
- **URL Previews**: Automatic Open Graph-based link previews in chat
- **Recipe Search**: Random and keyword-based food recipe lookup via Spoonacular
- **Random Facts**: Fetch random trivia facts

### Alerts & Announcements
- **Stream & Social Alerts**: Announce in a room when a Twitch channel goes live, a YouTube channel posts or streams,
  or a Twitter account tweets
- **Cross-room Announcements**: Announce tournaments and games (hangman included) in linked rooms, with a cooldown
  and a per-room filter

### Pokémon Showdown Integration
- **Ladder Tracking**: Monitor and display ladder rankings in real time
- **Battle Tracking**: Subscribe to new battles from a subset of players
- **Rank Display**: Fetch and show player ELO/GXE per format with a visual showcase
- **ELO History & Graphs**: Track ELO progression over time and render trend-line ladder graphs with mean and standard deviation
- **Smogon Usage Stats**: Look up Pokémon usage statistics per tier and month, and graph a Pokémon's usage over time
- **Team Samples**: Add, showcase, and manage team lists per tier
- **Team Previews**: Automatic previews of Pokepaste, Coup Critique and Showdown team links
- **Replay Previews**: Automatic rich preview cards for Pokémon Showdown replay links

### Room Management
- **Room Dashboard**: Overview of room configuration, diagnostics, and quick-start games
- **Room Config**: Tune parameters: locale, timezone, auto-correct, error messages, preview toggles, bucks, streaks
- **Custom Commands**: Add, edit, delete room-specific commands
- **Repeating Messages**: Schedule recurring announcements, persisted across restarts
- **Shop**: Virtual item shop with configurable listings
- **Polls**: Keep a history of created polls, and let users suggest polls (with bans for abuse)
- **Watchlist**: Per-room list of watched users, injected into the staff intro, with Discord alerts when one becomes active

### Chat Logs & Stats
- **Chat Logs**: Per-room message logging with S3 persistence
- **Activity Stats**: Line counts, daily activity graphs, activity heatmaps, and top-user leaderboards
- **Markov Sentences**: Generate sentences in the style of a room or a user from the logs

### Utilities
- **Timer / Reminder**: Set countdown timers in chat
- **Dictionary**: English word definitions
- **Crypto Prices**: Live price lookup for Bitcoin, Ethereum, and Dogecoin
- **Fuel Prices**: French fuel prices per city or area, from the government's open data
- **Pokémon Name Translation**: Cross-language Pokémon name lookup
- **Bug Report**: Report a bug from a panel, opened as a GitHub issue
- **Ship**: Compatibility score between two users
- **League of Legends**: Player rank lookup and recent match history via Riot Games API
- **Changelog**: Display the bot's recent changelog generated from git history
- **Pairings**: Generate and display bracket pairings

### Internationalization
- **6 supported languages**: english, french, spanish, italian, portuguese, and german
- **Per-room locale**: each room can set its own language independently

### Command Engine
- **Ordered per-room dispatch**: each room's messages are handled in arrival order, rooms run concurrently, and slow
  commands run in the background without holding up their room
- **Fuzzy autocorrect**: Levenshtein distance matching suggests the closest command on typo (configurable per room)
- **Message throttling**: outgoing message queue with cooldown and length limits to respect server constraints
- **Cancellable commands**: long-running commands can be listed and cancelled at runtime
- **Feature switches**: turn a feature category on or off at runtime
- **Admin tools**: handler dashboard, lag test, memory and system diagnostics

### Resilience & Templating
- **WebSocket auto-reconnect**: automatic reconnection with configurable error and lost-connection timeouts
- **Graceful shutdown**: pending writes are flushed on SIGTERM or Ctrl+C
- **Razor component HTML templates**: `.razor` templates compiled at build time and rendered with `HtmlRenderer` for rich HTML responses

### Per-room Configuration
- **Configurable settings**: each room independently controls locale, timezone, command autocorrect, error message visibility, previews, economy, streaks, and announcements
- **Persistent storage**: parameters are stored in the database via Entity Framework and applied at runtime

### Instrumentation & Observability
- **Structured logging**: Serilog with daily rolling file output and optional Grafana Loki sink
- **Metrics**: OpenTelemetry counters and histograms for messages received/sent, commands executed, errors, command duration, HTTP requests, and WebSocket reconnections

### Battle System
The bot plays Pokémon Showdown battles on its own. **Only generation 9 is supported**: its models are trained on
gen 9 random battles and its damage calculations use gen 9 data. Singles play best; doubles fall back on a simple
damage-based choice.

- **Challenges**: accepts any user's challenge. Random battles need no team; other formats use the team picked with
  `-useteam`, or else a saved team the server validates. Without one, the bot rejects the challenge and says why.
- **Laddering**: `-laddering <format>` searches a new battle after each one ends, staying under Showdown's search
  rate limit (`-laddering off` stops it). `-search <format>` starts a single search.
- **minipoke strategies**: small transformer models (~22M parameters) trained in [`tools/minipoke`](tools/minipoke/README.md)
  and exported to ONNX, run inside the bot:
  - `minipokev3-22m`: trained offline on human ladder replays
  - `minipokev3-22m+search` (default): the same model, looking one turn ahead on Showdown's simulator and playing
    the option its value head scores best (needs `ShowdownDirectory` and Node.js)
  - `minipokev2.1-22m+search`: the previous model, trained by self-play, with search
  - `damagecalc`: a damage-calc search of the turn, also what the models fall back on
- **Action guard**: vetoes moves that are sure to be wasted (healing at full HP, a maxed hazard, a move that already
  failed on the same target...)
- **Battle chat**: greets the opponent and now and then reacts to crits, hax and the result
- **Replay saving**: `-savereplays on` saves each battle's log and replay, used to train the models
- **Status**: `-battlestatus` shows the laddering state, rating and record per format and strategy; `-strategy`
  shows or switches the strategy

Battle commands are restricted to whitelisted users.

## License
MIT, see `LICENSE`.

## Roadmap
See `TODO.md`.
