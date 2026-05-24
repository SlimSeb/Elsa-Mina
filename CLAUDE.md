# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Elsa-Mina is a Pokémon Showdown chat bot written in C# (.NET 10.0). It connects via WebSocket, receives server messages as pipe-delimited strings, dispatches them to handlers, and executes commands in chat rooms.

## Commands

```bash
# Build, restore, test
./scripts/Build/restore.sh       # Restore NuGet packages
./scripts/Build/build.sh         # Build the solution
./scripts/Build/test.sh          # Run all tests

# Run a single test project
dotnet test test/ElsaMina.UnitTests/ElsaMina.UnitTests.csproj --no-restore --verbosity normal

# Run tests matching a specific name filter
dotnet test ElsaMina.slnx --filter "FullyQualifiedName~ToggleLadderTracker"

# Run the bot
cd src/ElsaMina.Console && dotnet run

# Database migrations (EF Core)
dotnet ef migrations add <MigrationName> --project src/ElsaMina.DataAccess
dotnet ef database update --project src/ElsaMina.DataAccess
dotnet ef migrations remove --project src/ElsaMina.DataAccess
```

## Architecture

### Project Layout

| Project | Role |
|---|---|
| `ElsaMina.Console` | Entry point: wires DI, reads `config.json`, starts the bot |
| `ElsaMina.Core` | Bot runtime, handlers, services, context system |
| `ElsaMina.Commands` | All command and handler implementations |
| `ElsaMina.DataAccess` | EF Core DbContext, models, migrations (PostgreSQL) |
| `ElsaMina.FileSharing` | S3 file upload abstraction |
| `ElsaMina.Sheets` | Google Sheets integration |
| `ElsaMina.Logging` | Thin logging abstraction over Serilog |

### Message Flow

```
WebSocket → IClient.MessageReceived
  → Bot.HandleReceivedMessageAsync (splits lines, tracks current room)
    → HandlerManager.HandleMessageAsync (runs all IHandler in parallel)
      → ChatMessageCommandHandler / PrivateMessageCommandHandler
        → CommandExecutor → ICommand.RunAsync(IContext)
```

### Handler System

- All handlers implement `IHandler` / extend `Handler`.
- `HandlerManager` resolves all `IHandler` instances from the DI container and runs them concurrently for every incoming message.
- Each handler filters on the message parts it cares about (e.g., `parts[1] == "c:"` for chat).
- Handlers are registered with `builder.RegisterHandler<T>()` in `CoreModule.cs` (core) or `CommandModule.cs` (feature).

### Command System

- Commands extend `Command` and are decorated with `[NamedCommand("name", Aliases = ["alias"])]`.
- Key overridable properties: `RequiredRank`, `IsAllowedInPrivateMessage`, `IsWhitelistOnly`, `IsPrivateMessageOnly`, `HelpMessageKey`, `RoomRestriction`.
- `context.Target` holds the argument string (everything after the command trigger + name).
- Commands are registered with `builder.RegisterCommand<T>()` in `CommandModule.cs`.
- The `ScriptCommand` is only registered in `#if DEBUG`.

### Context System

`IContext` is the interface commands receive. It provides:
- `context.Reply(msg)` / `context.ReplyHtml(html)` - send a response
- `context.ReplyLocalizedMessage(key, args...)` - send a localized response
- `context.GetString(key)` - get a localized string
- `context.Sender`, `context.Room`, `context.RoomId`, `context.Target`, `context.Command`
- `context.HasRankOrHigher(rank)` / `context.HasSufficientRankInRoom(roomId, rank)`
- `context.HandleErrorAsync(exception)` - standard error reply handling

Two concrete implementations: `RoomContext` and `PmContext`.

### Dependency Injection

Uses Autofac. Two main modules:
- `CoreModule` - all core services (bot, client, rooms, handlers, etc.)
- `CommandModule` - all commands, feature-level handlers, and feature services

`DependencyContainerService` wraps the Autofac container and is used for late-bound resolution (e.g., `HandlerManager` resolves handlers after startup).

### Localization

String resources are split by feature. `IResourcesService` aggregates all `ResourceManager` instances registered in DI and searches them in order.

| Location | Pattern | Used for |
|---|---|---|
| `src/ElsaMina.Core/Resources/Resources.{locale}.resx` | Core infrastructure strings: errors, room params, core handlers |
| `src/ElsaMina.Commands/{Feature}/Resources/{Feature}.{locale}.resx` | Feature strings for that feature's commands and handlers |

Supported locales: `en-US`, `fr-FR`, `es-ES`, `it-IT`, `pt-BR`, `de-DE`.

**Rule:** when adding strings for a command, add keys to `src/ElsaMina.Commands/{Feature}/Resources/{Feature}.{locale}.resx` for all 6 locales. Each feature's `ResourceManager` is registered in `CommandModule.cs` via the `FeatureResources` helper. Room locale is a configurable `Parameter`.

### HTML Templates

Rich HTML responses use RazorLight (`.cshtml` files). Templates live next to the command files in `ElsaMina.Commands/`. `ITemplatesManager.GetTemplateAsync(key, model)` renders them. Template keys correspond to the relative path under a `Templates/` directory.

### Room Parameters

Rooms have configurable parameters (defined in the `Parameter` enum: `Locale`, `TimeZone`, `HasCommandAutoCorrect`, `ShowErrorMessages`, `ShowTeamLinksPreview`, `ShowReplaysPreview`). Values are stored via `IRoomParameterStore` / `EfRoomParameterStore`.

### Async Query Pattern

`PendingQueryRequestsManager<TKey, TResult>` is used when the bot needs to send a query and await a server response asynchronously (fire-and-wait pattern with timeout).

## Adding a New Command

1. Create a class in the relevant subdirectory of `src/ElsaMina.Commands/`.
2. Decorate with `[NamedCommand("commandname")]` (add aliases as needed).
3. Extend `Command`, override `RequiredRank` (default is `Admin`), and implement `RunAsync`.
4. Register in `CommandModule.cs`: `builder.RegisterCommand<MyCommand>();`
5. Add localization keys to `src/ElsaMina.Commands/{Feature}/Resources/{Feature}.{locale}.resx` for all 6 locales (`en-US`, `fr-FR`, `es-ES`, `it-IT`, `pt-BR`, `de-DE`).

## Adding a New Handler

1. Create a class extending `Handler` in `src/ElsaMina.Commands/` or `src/ElsaMina.Core/Handlers/`.
2. Implement `HandleReceivedMessageAsync(string[] parts, string roomId, CancellationToken)`.
3. Register with `builder.RegisterHandler<MyHandler>()` in `CommandModule.cs` or `CoreModule.cs`.

## Code Style Conventions

- **One class per file**: every class, record, or interface must live in its own dedicated `.cs` file named after the type. Never define multiple types in a single file.
- **No single-letter variables**: use descriptive names everywhere. Exception: integer loop indices (`i`, `j`, `k`) are allowed.

## Testing Conventions

- Framework: NUnit 3 + NSubstitute.
- Test naming pattern: `Test_MethodName_ShouldExpectedBehavior_WhenCondition`.
- `SetUp` method creates the SUT and substitutes for all dependencies.
- `Log.Configuration` must be substituted in `SetUpFixture.cs` (already done globally).
- Unit tests go in `test/ElsaMina.UnitTests/`, mirroring the source structure.

## Configuration

`src/ElsaMina.Console/config.json` (copied from `example.config.json`). Key fields: `Host`, `Port`, `Name`, `Password`, `Trigger` (command prefix, default `-`), `Rooms`, `Whitelist`, `DefaultRoom`, `DefaultLocaleCode`, `ConnectionString`, and optional API keys.


## Commit Format
```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types
- feat: New feature
- fix: Bug fix
- docs: Documentation changes
- style: Code style changes
- refactor: Code refactoring
- test: Adding or modifying tests
- chore: Maintenance tasks
- perf: Performance improvements

### Example Output
```
feat(auth): add password reset functionality

- Add forgot password form
- Implement email verification flow
- Add password reset endpoint
```