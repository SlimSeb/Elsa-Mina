# Elsa-Mina

[![deploy](https://github.com/SlimSeb/Elsa-Mina/actions/workflows/deploy.yml/badge.svg)](https://github.com/SlimSeb/Elsa-Mina/actions/workflows/deploy.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=slimf1_Elsa-Mina&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=slimf1_Elsa-Mina)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=slimf1_Elsa-Mina&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=slimf1_Elsa-Mina)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=slimf1_Elsa-Mina&metric=coverage)](https://sonarcloud.io/summary/new_code?id=slimf1_Elsa-Mina)

A chat bot for Pokémon Showdown, used in the French Room and private communities.

**[Live Demo](https://psim.us/fr)** • **[Report Bug](https://github.com/SlimSeb/Elsa-Mina/issues)**

## Prerequisites
- .NET 10.0 or higher
- PostgreSQL 12 or higher
- Docker (optional, for containerized deployment)

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
./scripts/restore.sh
./scripts/build.sh
```

4. Run the bot
```bash
cd src/ElsaMina.Console
dotnet run ElsaMina.Console.dll
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
Config lives at `src/ElsaMina.Console/config.json`. Use the example file and fill in the relevant values..

## Database
We use PostgreSQL for persistence. The database connection is managed by Entity Framework Core.
To update the database schema, run the following commands in the root directory:

```bash
dotnet ef migrations add <MigrationName> --project src/ElsaMina.DataAccess
dotnet ef database update --project src/ElsaMina.DataAccess
dotnet ef migrations remove --project src/ElsaMina.DataAccess
```

## Testing
The code is unit-tested with NUnit and NSubstitute. Two test projects are available:
- `ElsaMina.UnitTests` for minimal unit tests.
- `ElsaMina.IntegrationTests` for more complex integration tests.

To run the tests, run the following command in the root directory:
```bash
./scripts/test.sh
```

## Scripts
The following scripts are available:
```bash
./scripts/Build/restore.sh      # Restore NuGet packages
./scripts/Build/build.sh        # Build the solution
./scripts/Build/test.sh         # Run all tests
./scripts/Build/publish.sh      # Publish for deployment
./scripts/Build/full_publish.sh # Do a full restore, test, build and publish
```

## Features

### Games & Entertainment
- **Connect Four**: 2-player board game with join/play/forfeit flow
- **Lights Out**: Puzzle game with leaderboard tracking
- **Voltorb Flip**: Pokémon HGSS' Voltorb Flip with mark and leaderboard support
- **Guessing Games**: Multiple modes: Pokémon descriptions, cries, country capitals, and more
- **Poke Race**: Multi-player racing mini-game
- **Slots**: Slot machine mini-game

### Tournaments
- **Weekly Tournament Templates**: 23+ pre-configured formats (OU, RU, BH, AAA, 1v1, ZU, and more)
- **Random Tournaments**: Create random tournament
- **Tournament Configuration**: Save, edit, and launch custom tournament setups
- **Tournament Leaderboard**: Track top tournament players per room

### Arcade Commands
- **Event Registration**: Join/leave arcade events, manage participants
- **Points & Leaderboard**: Track scores across arcade events
- **Achievement Tiers**: Configurable level tiers with display
- **Hall of Fame**: Persistent records synced to Google Sheets
- **CAA Integration**: Dedicated CAA (Advent calendar) event tracking

### User Profiles & Social
- **Profiles**: Display user profile with avatar, title, and badge showcase
- **Badges & Trophies**: Create, award, and revoke badges with a management panel
- **Custom Name Colors**: Per-user HTML color for display names
- **Seen**: Track when a user was last active
- **Playtime Tracking**: Per-user playtime with top leaderboard
- **Join Phrases**: Personalized greeting when entering a room
- **Alts Detection**: Display known alternate accounts

### AI & Media
- **AI Chat**: Conversational AI with multi-turn history (OpenAI / Google / Mistral)
- **Text-to-Speech**: Generate audio responses from text
- **YouTube / Dailymotion Search**: Embed video search results
- **Lyrics Search**: Look up song lyrics via Genius
- **Wikipedia / Bulbapedia / Pokepedia**: Inline wiki searches

### Pokémon Showdown Integration
- **Ladder Tracking**: Monitor and display ladder rankings in real time
- **Battle Tracking**: Subscribe to new battles from a subset of players
- **Rank Display**: Fetch and show player ELO/GXE per format
- **Team Samples**: Add, showcase, and manage team lists per tier

### Room Management
- **Room Dashboard**: Overview of room configuration and state
- **Room Config**: Tune parameters: locale, timezone, auto-correct, error messages, preview toggles
- **Custom Commands**: Add, edit, delete room-specific commands
- **Repeating Messages**: Schedule recurring announcements
- **Shop**: Virtual item shop with configurable listings
- **Polls**: Keep a history of created polls

### Utilities
- **Timer / Reminder**: Set countdown timers in chat
- **Dictionary**: English word definitions
- **Bitcoin Price**: Live BTC price lookup
- **Pokémon Name Translation**: Cross-language Pokémon name lookup
- **Bug Report**: Inline issue reporting shortcut

### Internationalization
- **6 supported languages**: english, french, spanish, italian, portuguese, and german
- **Per-room locale**: each room can set its own language independently

### Instrumentation & Observability
- **Structured logging**: Serilog with daily rolling file output and optional Grafana Loki sink
- **Metrics**: OpenTelemetry counters and histograms for messages received/sent, commands executed, errors, command duration, HTTP requests, and WebSocket reconnections

### Command Engine
- **Fuzzy autocorrect**: Levenshtein distance matching suggests the closest command on typo (configurable per room)
- **Message throttling**: outgoing message queue with cooldown and length limits to respect server constraints
- **Cancellable commands**: long-running commands can be listed and cancelled at runtime

### Resilience & Templating
- **WebSocket auto-reconnect**: automatic reconnection with configurable error and lost-connection timeouts
- **RazorLight HTML templates**: `.cshtml` templates pre-compiled at startup and cached in memory for rich HTML responses

### Per-room Configuration
- **Configurable settings**: each room independently controls locale, timezone, command autocorrect, error message visibility, team link previews, and replay previews
- **Persistent storage**: parameters are stored in the database via Entity Framework and applied at runtime

### Battle System
- **Automated battle bot**: accepts random battle challenges and plays out matches autonomously
> [!WARNING]  
> The battle system is currently in development and may not be stable.

### Watchlist
- **Watchlist monitoring**: maintain a per-room list of watched users with associated context
- **Staff intro integration**: automatically injects the watchlist into the room's staff intro display
- **Discord notifications**: sends alerts when a watched user becomes active

## License
MIT, see `LICENSE`.

## Roadmap
See `TODO.md`.
