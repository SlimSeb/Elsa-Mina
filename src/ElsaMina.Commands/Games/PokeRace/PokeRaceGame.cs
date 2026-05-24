using ElsaMina.Core.Contexts;
using System.Threading;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.PokeRace;

public class PokeRaceGame : Game, IPokeRaceGame
{
    private static int _nextGameId;

    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly ISystemService _systemService;
    private readonly PeriodicTimerRunner _autoStartTimer;
    private readonly PeriodicTimerRunner _raceUpdateTimer;

    private readonly Dictionary<string, (string Name, string Pokemon)> _players = new();
    private readonly Dictionary<string, double> _positions = new();
    private readonly List<string> _finished = new();
    private readonly List<string> _allEvents = new();

    private int _turn;

    [UsedImplicitly]
    public PokeRaceGame(IRandomService randomService, ITemplatesManager templatesManager, ISystemService systemService)
        : this(randomService, templatesManager, PokeRaceConstants.AUTO_START_DELAY, PokeRaceConstants.UPDATE_INTERVAL,
            systemService)
    {
    }

    public PokeRaceGame(IRandomService randomService, ITemplatesManager templatesManager, TimeSpan autoStartDelay,
        TimeSpan updateInterval, ISystemService systemService)
    {
        _randomService = randomService;
        _templatesManager = templatesManager;
        _systemService = systemService;
        GameId = Interlocked.Increment(ref _nextGameId);
        _autoStartTimer = new PeriodicTimerRunner(autoStartDelay, OnAutoStartAsync, runOnce: true);
        _raceUpdateTimer = new PeriodicTimerRunner(updateInterval, OnRaceUpdateAsync);
    }

    public int GameId { get; }
    public IContext Context { get; set; }
    public IReadOnlyDictionary<string, (string Name, string Pokemon)> Players => _players;
    public override string Identifier => nameof(PokeRaceGame);

    private string HtmlId => $"pokerace-{GameId}";

    public async Task BeginJoinPhaseAsync()
    {
        var html = await BuildLobbyHtmlAsync();
        Context.SendUpdatableHtml(HtmlId, html, false);
        _autoStartTimer.Start();
    }

    public async Task<(bool Success, string MessageKey, object[] Args)> JoinRaceAsync(string userName,
        string pokemonName)
    {
        if (IsStarted)
        {
            return (false, "pokerace_race_already_started", []);
        }

        var userId = userName.ToLowerAlphaNum();
        if (_players.TryGetValue(userId, out var value))
        {
            return (false, "pokerace_join_already_chosen", [value.Pokemon]);
        }

        if (!PokeRaceConstants.RACE_POKEMON.ContainsKey(pokemonName))
        {
            var available = string.Join(", ", PokeRaceConstants.RACE_POKEMON.Keys);
            return (false, "pokerace_join_invalid_pokemon", [pokemonName, available]);
        }

        if (_players.Values.Any(player => player.Pokemon == pokemonName))
        {
            return (false, "pokerace_join_pokemon_taken", [pokemonName]);
        }

        _players[userId] = (userName, pokemonName);
        var html = await BuildLobbyHtmlAsync();
        Context.SendUpdatableHtml(HtmlId, html, true);
        return (true, "pokerace_join_success", [userName, pokemonName]);
    }

    public async Task StartRaceAsync()
    {
        if (IsStarted || _players.Count < PokeRaceConstants.MIN_PLAYERS)
        {
            return;
        }

        _autoStartTimer.Stop();
        OnStart();
        InitializeRaceData();

        var html = await BuildRaceStartHtmlAsync();
        Context.SendUpdatableHtml(HtmlId, html, true);
        await _systemService.SleepAsync(TimeSpan.FromSeconds(3));

        _raceUpdateTimer.Start();
    }

    public void Cancel()
    {
        _autoStartTimer.Stop();
        _raceUpdateTimer.Stop();
        OnEnd();
    }

    private void InitializeRaceData()
    {
        _positions.Clear();
        _finished.Clear();
        _allEvents.Clear();
        _turn = 0;

        var pokemonBySpeed = _players.Values
            .Select(player => player.Pokemon)
            .OrderByDescending(pokemon => PokeRaceConstants.RACE_POKEMON[pokemon].Speed)
            .ToList();

        for (var i = 0; i < pokemonBySpeed.Count; i++)
        {
            _positions[pokemonBySpeed[i]] = -i * 0.1;
        }
    }

    private async Task OnAutoStartAsync()
    {
        if (IsStarted)
            return;

        if (_players.Count >= PokeRaceConstants.MIN_PLAYERS)
        {
            await StartRaceAsync();
        }
        else
        {
            Context.ReplyLocalizedMessage("pokerace_auto_start_not_enough_players", PokeRaceConstants.MIN_PLAYERS);
            Cancel();
        }
    }

    private async Task OnRaceUpdateAsync()
    {
        if (IsEnded)
        {
            return;
        }

        _turn++;
        var turnEvents = new List<string>();

        var sortedByPosition = _positions
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();

        var activePokemon = sortedByPosition.Where(pokemon => !_finished.Contains(pokemon)).ToList();
        var leader = activePokemon.Count > 0 ? activePokemon[0] : null;
        var trailing = activePokemon.Count > 1 ? activePokemon[^1] : null;
        var gap = leader != null && trailing != null ? _positions[leader] - _positions[trailing] : 0;

        foreach (var pokemon in sortedByPosition)
        {
            if (_finished.Contains(pokemon))
            {
                continue;
            }

            var baseMove = 1.0 + (PokeRaceConstants.RACE_POKEMON[pokemon].Speed - 100.0) / 200.0;
            var randomFactor = _randomService.NextDouble() * (1.8 - 0.7) + 0.7;

            RaceEvent raceEvent = null;
            if (_randomService.NextDouble() < 0.3)
            {
                raceEvent = ChooseEvent(pokemon, leader, trailing, gap);
                turnEvents.Add(raceEvent.TextTemplate.Replace("{pokemon}", pokemon));
            }

            var movement = Math.Max(0.3, baseMove * randomFactor + (raceEvent?.Effect ?? 0));
            _positions[pokemon] += movement;

            if (_positions[pokemon] >= PokeRaceConstants.RACE_LENGTH)
            {
                _positions[pokemon] = PokeRaceConstants.RACE_LENGTH;
                _finished.Add(pokemon);
            }
        }

        if (turnEvents.Count > 0)
        {
            _allEvents.AddRange(turnEvents.Select(eventText => $"Tour {_turn}: {eventText}"));
            if (_allEvents.Count > PokeRaceConstants.MAX_RECENT_EVENTS)
            {
                _allEvents.RemoveRange(0, _allEvents.Count - PokeRaceConstants.MAX_RECENT_EVENTS);
            }
        }

        if (_finished.Count == _positions.Count)
        {
            _raceUpdateTimer.Stop();
            var html = await BuildRaceResultsHtmlAsync();
            Context.SendUpdatableHtml(HtmlId, html, true);
            OnEnd();
        }
        else
        {
            var html = await BuildRaceUpdateHtmlAsync();
            Context.SendUpdatableHtml(HtmlId, html, true);
        }
    }

    private RaceEvent ChooseEvent(string pokemon, string leader, string trailing, double gap)
    {
        if (pokemon == leader && gap > 2 && _randomService.NextDouble() < 0.4)
        {
            return PokeRaceConstants.RACE_EVENTS.FirstOrDefault(evt => evt.EventType == "leader_penalty")
                   ?? _randomService.RandomElement(PokeRaceConstants.RACE_EVENTS.Where(evt => evt.EventType == "slow")
                       .ToList());
        }

        if (pokemon == trailing && gap > 3 && _randomService.NextDouble() < 0.5)
        {
            return PokeRaceConstants.RACE_EVENTS.FirstOrDefault(evt => evt.EventType == "trailing_boost")
                   ?? _randomService.RandomElement(PokeRaceConstants.RACE_EVENTS.Where(evt => evt.EventType == "boost")
                       .ToList());
        }

        return _randomService.RandomElement(PokeRaceConstants.RACE_EVENTS);
    }

    private PokeRaceModel BuildModel() => new()
    {
        Culture = Context.Culture,
        Players = _players,
        ChosenPokemons = _players.Values.Select(p => p.Pokemon).ToHashSet(),
        Finished = _finished.ToList(),
        SortedPositions = _positions.OrderByDescending(kvp => kvp.Value).Select(kvp => (kvp.Key, kvp.Value)).ToList(),
        AllEvents = _allEvents.ToList(),
        Turn = _turn
    };

    private async Task<string> BuildLobbyHtmlAsync() =>
        (await _templatesManager.GetTemplateAsync("Games/PokeRace/PokeRaceLobby", BuildModel())).RemoveNewlines();

    private async Task<string> BuildRaceStartHtmlAsync() =>
        (await _templatesManager.GetTemplateAsync("Games/PokeRace/PokeRaceStart", BuildModel())).RemoveNewlines();

    private async Task<string> BuildRaceUpdateHtmlAsync() =>
        (await _templatesManager.GetTemplateAsync("Games/PokeRace/PokeRaceUpdate", BuildModel())).RemoveNewlines();

    private async Task<string> BuildRaceResultsHtmlAsync() =>
        (await _templatesManager.GetTemplateAsync("Games/PokeRace/PokeRaceResults", BuildModel())).RemoveNewlines();
}