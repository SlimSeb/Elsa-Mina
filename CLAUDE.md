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
./scripts/Build/code-analysis.sh # Fail on any Sonar rule issue (same check as CI)

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
| `ElsaMina.Console` | Composition root: reads `config.json` into `Configuration`, registers every module, runs the message pump and shutdown |
| `ElsaMina.Core` | Bot runtime: dispatch, handlers, commands, contexts, rooms, lifecycle, templates, plus shared services (dex, Smogon usage, team packing, language models). References only `Logging` |
| `ElsaMina.Commands` | Every feature: commands, feature handlers and feature services |
| `ElsaMina.Battles` | Autonomous battle bot: protocol parsing, simulation, and decision strategies |
| `ElsaMina.DataAccess` | EF Core DbContext, models, migrations (PostgreSQL), and Core's persistence ports (`EfRoomParameterRepository`) |
| `ElsaMina.Cloud` | S3 file upload, Google Drive, and Google Sheets integration |
| `ElsaMina.Logging` | Thin logging abstraction over Serilog |

**Dependency rules** (enforced by `test/ElsaMina.UnitTests/Architecture/ProjectDependenciesTest.cs`):
- Core knows no feature, infrastructure or host project, and no EF/Google/AWS package. When Core needs something a feature or infrastructure provides, it defines an interface (a port) and the other project implements it.
- Feature projects (`Commands`, `Battles`) never reference each other; code both need goes to Core.
- Only `Console` references everything.

### Message Flow

```
WebSocket → IClient.Messages
  → BotHost message pump (reads frames in order)
    → IncomingMessageDispatcher (one ordered lane per room, rooms run concurrently)
      → Bot.HandleReceivedMessageAsync (splits lines, room from the >room header, "lobby" when absent)
        → HandlerManager.HandleMessageAsync (runs the matching IHandler concurrently for that line)
          → ChatMessageCommandHandler / PrivateMessageCommandHandler
            → CommandExecutor → ICommand.RunAsync(IContext), in the background
Replies → Bot.Send → OutgoingMessageQueue (one queue, messages one cooldown apart) → IClient
```

- A room's frames are handled one after the other, in arrival order. Never make a handler wait for a message that arrives in the **same** room: it is queued behind the handler and the wait only ends at its timeout. Waiting for a global message (`queryresponse`, `pm`) is fine.
- Commands run in the background so a slow command never holds up its room. Commands that change in-memory state shared with the room's other messages (games) set `RunsInMessageOrder` and run in order instead.
- Frames without a `>room` header are the lobby's or global (Showdown only omits the header for those).

### Handler System

- All handlers implement `IHandler` / extend `Handler`.
- `HandlerManager` gets every registered `IHandler` once, at startup, and runs the ones whose `HandledMessageTypes` match the line. Build the `HandledMessageTypes` set once (`{ get; } = ...`), not per call.
- Each handler filters on the message parts it cares about (e.g., `parts[1] == "c:"` for chat). Message handlers built on `MessageHandler` share one `IContext` per line.
- Register with `builder.RegisterHandler<T>()` in `CoreModule.cs` (runtime) or the feature's module under `ElsaMina.Commands/Modules/`.

### Command System

- Commands extend `Command` and are decorated with `[NamedCommand("name", Aliases = ["alias"])]`.
- Key overridable properties: `RequiredRank`, `IsAllowedInPrivateMessage`, `IsWhitelistOnly`, `IsPrivateMessageOnly`, `HelpMessageKey`, `RoomRestriction`, `RunsInMessageOrder`.
- Commands are singletons indexed by `ICommandRegistry` under their name and aliases: never keep per-call state in fields.
- `Category` (feature switches, command list) comes from the namespace (`ElsaMina.Commands.{Feature}`, or the project name such as `Battles`), or from `[NamedCommand(Category = ...)]`.
- Failures are reported to the user by `CommandExecutor` (`context.HandleErrorAsync`); commands do not need to catch everything.
- Names that are not registered commands go to the `IDynamicCommandProvider`s (custom commands, tour configs), tried in registration order.
- `context.Target` holds the argument string (everything after the command trigger + name).

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

Uses Autofac. `ContainerBootstrapper` (Console) registers the modules: `DataAccessModule`, `CloudModule`, `CoreModule`, `BattlesModule`, `CommandModule` (which registers the feature modules in `ElsaMina.Commands/Modules/`).

- Inject dependencies through constructors only; there is no service locator. To create objects on demand inject `Func<T>` (games: `Func<WordleGame>`), and to break a construction cycle inject `Lazy<T>`.
- Register objects created on demand that the container should not keep alive (games) with `.ExternallyOwned()`.
- `test/ElsaMina.UnitTests/Startup/ContainerBootstrapperTest.cs` builds the real container and resolves every handler, command and lifecycle participant: run it after changing registrations.

### Lifecycle

Services with work to do when the bot starts (load data, start a polling loop) or stops (flush pending writes) implement `IBotLifecycleParticipant` and are registered with `.As<IBotLifecycleParticipant>()`. The bot runs every participant's `OnStartingAsync` before connecting and awaits every `OnExitingAsync` on shutdown (SIGTERM or Ctrl+C), bounded by a timeout. Do not use Autofac `AutoActivate`/`OnActivating` to start work.

### Configuration

`config.json` is read into `Configuration` (Console), which implements each project's settings interface: `IConfiguration` (Core: connection, identity, rooms, language model keys), `ICommandsConfiguration` (feature API keys and timings), `IDatabaseConfiguration`, and `IS3CredentialsProvider` / `IGoogleServiceAccountConfiguration` (Cloud). A new setting goes on the interface of the project that uses it, and on `Configuration`.

### Localization

String resources are split by feature. `IResourcesService` aggregates all `ResourceManager` instances registered in DI and searches them in order.

| Location | Pattern | Used for |
|---|---|---|
| `src/ElsaMina.Core/Resources/Resources.{locale}.resx` | Core runtime strings: errors, Core's room parameters, core handlers |
| `src/ElsaMina.Commands/{Feature}/Resources/{Feature}.{locale}.resx` | Feature strings for that feature's commands, handlers and room parameters |

Supported locales: `en-US`, `fr-FR`, `es-ES`, `it-IT`, `pt-BR`, `de-DE`.

**Rule:** when adding strings for a command, add keys to `src/ElsaMina.Commands/{Feature}/Resources/{Feature}.{locale}.resx` for all 6 locales. `CommandModule` discovers and registers every feature's `ResourceManager`. Room locale is a configurable `Parameter`.

### HTML Templates

Rich HTML responses use Razor components (`.razor` files) compiled at build time (no runtime compilation) and rendered to strings with `HtmlRenderer`. Templates live next to the command files in `ElsaMina.Commands/` and inherit `LocalizableTemplatePage<TViewModel>` (or `TemplatePage<TModel>` for models without a culture). `_Imports.razor` puts them under the `ElsaMina.Templates` namespace, and the template key is the folder path plus file name (e.g. `Games/Wordle/WordleBoard`). `ITemplatesManager.GetTemplateAsync(key, model)` renders them. Use `@Raw(html)` for unencoded HTML and `<ElsaMina.Templates.Badges.Badge Model="@(badge)" />` to render one template inside another (inside a code block such as `@foreach`, wrap it in `<text>...</text>` so the line indentation is not rendered). Inside an attribute, combine values into a single expression (`style="@(a + b)"`), the component compiler rejects adjacent ones like `@a@b`.

### Room Parameters

Rooms have configurable parameters. A `Parameter` is identified by the short key its value is stored under (`"loc"`, `"bck"`...) and has a readable name staff can type in the room configuration command. Core defines `Parameter.Locale`, `TimeZone`, `HasCommandAutoCorrect` and `ShowErrorMessages`; each feature defines its own in a `{Feature}RoomParameters` class implementing `IRoomParameterProvider` (e.g. `EconomyRoomParameters.BucksEnabled`), registered with `.As<IRoomParameterProvider>()`. Never change an existing identifier: stored values would be orphaned. Values go through `IRoomParameterStore` (`RoomParameterStore`), persisted by `IRoomParameterRepository` (`EfRoomParameterRepository` in DataAccess).

### Async Query Pattern

`PendingQueryRequestsManager<TKey, TResult>` is used when the bot needs to send a query and await a server response asynchronously (fire-and-wait pattern with timeout). Await it from a command or from a handler waiting on a global response, never from a handler waiting on a message of its own room (see Message Flow).

## Adding a New Command

1. Create a class in the relevant subdirectory of `src/ElsaMina.Commands/`.
2. Decorate with `[NamedCommand("commandname")]` (add aliases as needed).
3. Extend `Command` (or `GameCommand` for a command that changes a game's state), override `RequiredRank` (default is `Admin`), and implement `RunAsync`.
4. Register in the feature's module under `ElsaMina.Commands/Modules/`: `builder.RegisterCommand<MyCommand>();`
5. Add localization keys to `src/ElsaMina.Commands/{Feature}/Resources/{Feature}.{locale}.resx` for all 6 locales (`en-US`, `fr-FR`, `es-ES`, `it-IT`, `pt-BR`, `de-DE`).

## Adding a New Game

- Commands that start, play or end the game extend `GameCommand`, so they run in order with the room's other messages. Read-only commands (leaderboards) extend `Command`. A game command must not wait for long inline (a countdown, an animation): start that work without awaiting it.
- Create the game through an injected `Func<MyGame>` and register it with `builder.RegisterType<MyGame>().AsSelf().ExternallyOwned();`.
- The command that starts it must check whether games are muted in the room before starting. Inject `IArcadeEventsService` and, for room (non-PM) starts, bail out early when games are muted:

```csharp
if (_arcadeEventsService.AreGamesMuted(context.RoomId))
{
    context.ReplyLocalizedMessage("games_muted_event");
    return;
}
```

The `games_muted_event` key already exists in the `Games` feature resx files for all 6 locales. Existing games (Tarot, VoltorbFlip, Wordle, Semantix, Blackjack, FloodIt, LightsOut, TwentyFortyEight, Slots) follow this pattern.

## Adding a New Handler

1. Create a class extending `Handler` in `src/ElsaMina.Commands/` or `src/ElsaMina.Core/Handlers/`.
2. Implement `HandleReceivedMessageAsync(string[] parts, string roomId, CancellationToken)`.
3. Register with `builder.RegisterHandler<MyHandler>()` in the feature's module or `CoreModule.cs`.

## Code Style Conventions

- **One class per file**: every class, record, or interface must live in its own dedicated `.cs` file named after the type. Never define multiple types in a single file.
- **No single-letter variables**: use descriptive names everywhere. Exception: integer loop indices (`i`, `j`, `k`) are allowed.
- **Sonar rules**: every project builds with `SonarAnalyzer.CSharp` (`Directory.Build.props`), the rules SonarCloud runs. Sonar warnings fail the `code-analysis` CI job, so fix them; when a rule is wrong for a line, suppress it there with `#pragma warning disable Sxxxx // why`. Rules off for the whole repository are in `.editorconfig` (and in `sonar.yml` for SonarCloud).

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