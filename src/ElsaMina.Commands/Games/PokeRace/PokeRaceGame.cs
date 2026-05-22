using ElsaMina.Core.Contexts;
using System.Threading;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.PokeRace;

public class PokeRaceGame : Game, IPokeRaceGame
{
    private const string RACE_OUTER_DIV = "<div style=\"background-color: #333; color: white; padding: 10px; border-radius: 5px;\">";
    private const string CLOSING_DIV = "</div>";

    private static int _nextGameId;

    private readonly IRandomService _randomService;
    private readonly PeriodicTimerRunner _autoStartTimer;
    private readonly PeriodicTimerRunner _raceUpdateTimer;

    private readonly Dictionary<string, (string Name, string Pokemon)> _players = new();
    private readonly Dictionary<string, double> _positions = new();
    private readonly List<string> _finished = new();
    private readonly List<string> _allEvents = new();

    private int _turn;

    [UsedImplicitly]
    public PokeRaceGame(IRandomService randomService)
        : this(randomService, PokeRaceConstants.AUTO_START_DELAY, PokeRaceConstants.UPDATE_INTERVAL)
    {
    }

    public PokeRaceGame(IRandomService randomService, TimeSpan autoStartDelay, TimeSpan updateInterval)
    {
        _randomService = randomService;
        GameId = Interlocked.Increment(ref _nextGameId);
        _autoStartTimer = new PeriodicTimerRunner(autoStartDelay, OnAutoStartAsync, runOnce: true);
        _raceUpdateTimer = new PeriodicTimerRunner(updateInterval, OnRaceUpdateAsync);
    }

    public int GameId { get; }
    public IContext Context { get; set; }
    public IReadOnlyDictionary<string, (string Name, string Pokemon)> Players => _players;
    public override string Identifier => nameof(PokeRaceGame);

    private string HtmlId => $"pokerace-{GameId}";

    public void BeginJoinPhase()
    {
        Context.SendUpdatableHtml(HtmlId, BuildLobbyHtml(), false);
        _autoStartTimer.Start();
    }

    public (bool Success, string MessageKey, object[] Args) JoinRace(string userName, string pokemonName)
    {
        if (IsStarted)
            return (false, "pokerace_race_already_started", []);

        var userId = userName.ToLowerAlphaNum();
        if (_players.ContainsKey(userId))
            return (false, "pokerace_join_already_chosen", [_players[userId].Pokemon]);

        if (!PokeRaceConstants.RACE_POKEMON.ContainsKey(pokemonName))
        {
            var available = string.Join(", ", PokeRaceConstants.RACE_POKEMON.Keys);
            return (false, "pokerace_join_invalid_pokemon", [pokemonName, available]);
        }

        if (_players.Values.Any(player => player.Pokemon == pokemonName))
            return (false, "pokerace_join_pokemon_taken", [pokemonName]);

        _players[userId] = (userName, pokemonName);
        Context.SendUpdatableHtml(HtmlId, BuildLobbyHtml(), true);
        return (true, "pokerace_join_success", [userName, pokemonName]);
    }

    public async Task StartRaceAsync()
    {
        if (IsStarted || _players.Count < PokeRaceConstants.MIN_PLAYERS)
            return;

        _autoStartTimer.Stop();
        OnStart();
        InitializeRaceData();

        Context.SendUpdatableHtml(HtmlId, BuildRaceStartHtml(), true);
        await Task.Delay(3000);

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

    private Task OnRaceUpdateAsync()
    {
        if (IsEnded)
            return Task.CompletedTask;

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
                continue;

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
                _allEvents.RemoveRange(0, _allEvents.Count - PokeRaceConstants.MAX_RECENT_EVENTS);
        }

        if (_finished.Count == _positions.Count)
        {
            _raceUpdateTimer.Stop();
            Context.SendUpdatableHtml(HtmlId, BuildRaceResultsHtml(), true);
            OnEnd();
        }
        else
        {
            Context.SendUpdatableHtml(HtmlId, BuildRaceUpdateHtml(), true);
        }

        return Task.CompletedTask;
    }

    private RaceEvent ChooseEvent(string pokemon, string leader, string trailing, double gap)
    {
        if (pokemon == leader && gap > 2 && _randomService.NextDouble() < 0.4)
        {
            return PokeRaceConstants.RACE_EVENTS.FirstOrDefault(evt => evt.EventType == "leader_penalty")
                   ?? _randomService.RandomElement(PokeRaceConstants.RACE_EVENTS.Where(evt => evt.EventType == "slow").ToList());
        }

        if (pokemon == trailing && gap > 3 && _randomService.NextDouble() < 0.5)
        {
            return PokeRaceConstants.RACE_EVENTS.FirstOrDefault(evt => evt.EventType == "trailing_boost")
                   ?? _randomService.RandomElement(PokeRaceConstants.RACE_EVENTS.Where(evt => evt.EventType == "boost").ToList());
        }

        return _randomService.RandomElement(PokeRaceConstants.RACE_EVENTS);
    }

    private string BuildLobbyHtml()
    {
        var html = new System.Text.StringBuilder();
        html.Append(RACE_OUTER_DIV);
        html.Append("<h3 style=\"text-align: center; margin: 0;\">🏁 Course de Pokémon 🏁</h3>");
        html.Append("<p style=\"text-align: center;\">Une nouvelle course de Pokémon va commencer!</p>");

        html.Append("<table style=\"margin: 0 auto; border-collapse: separate; border-spacing: 8px 4px;\">");
        var pokemonList = PokeRaceConstants.RACE_POKEMON.ToList();
        var numRows = (pokemonList.Count + 4) / 5;

        for (var row = 0; row < numRows; row++)
        {
            html.Append("<tr>");
            for (var col = 0; col < 5; col++)
            {
                var idx = row * 5 + col;
                if (idx < pokemonList.Count)
                {
                    var (pokemonName, data) = pokemonList[idx];
                    var isChosen = _players.Values.Any(player => player.Pokemon == pokemonName);
                    var opacity = isChosen ? " opacity: 0.4;" : "";
                    html.Append($"<td style=\"text-align: center; padding: 2px; width: 80px;{opacity}\">");
                    html.Append($"<img src=\"{data.MiniSprite}\" width=\"32\" height=\"32\"><br>");
                    html.Append($"<b>{pokemonName}</b><br>");
                    html.Append($"<em style=\"color: gray\">Vit {data.Speed}</em><br>{data.Type}");
                    html.Append("</td>");
                }
                else
                {
                    html.Append("<td style=\"width: 80px;\"></td>");
                }
            }
            html.Append("</tr>");
        }
        html.Append("</table>");

        if (_players.Count > 0)
        {
            html.Append("<p style=\"text-align: center;\"><b>Participants:</b></p>");
            html.Append("<div style=\"display: flex; flex-wrap: wrap; justify-content: center;\">");
            foreach (var (_, (name, pokemon)) in _players)
            {
                var color = name.ToColorHexCodeWithCustoms();
                html.Append("<div style=\"text-align: center; padding: 5px; margin: 0 8px;\">");
                html.Append($"<img src=\"{PokeRaceConstants.RACE_POKEMON[pokemon].MiniSprite}\" width=\"40\" height=\"40\"><br>");
                html.Append($"<b style=\"color: {color}\">{name}</b><br>");
                html.Append($"<span>{pokemon}</span>");
                html.Append(CLOSING_DIV);
            }
            html.Append(CLOSING_DIV);
        }

        html.Append("<p style=\"text-align: center;\">Pour participer, tapez <b>-racejoin [Pokémon]</b></p>");
        html.Append("<p style=\"text-align: center;\">La course commencera dans 60 secondes ou quand quelqu'un tapera <b>-racestart</b></p>");
        html.Append(CLOSING_DIV);
        return html.ToString();
    }

    private string BuildRaceStartHtml()
    {
        var html = new System.Text.StringBuilder();
        html.Append(RACE_OUTER_DIV);
        html.Append("<h3 style=\"text-align: center; margin: 0;\">🏁 Course de Pokémon 🏁</h3>");
        html.Append("<p style=\"text-align: center;\">Les Pokémon se préparent sur la ligne de départ!</p>");

        html.Append("<table style=\"margin: 0 auto; border-collapse: separate; border-spacing: 8px 4px;\">");
        var playersList = _players.Values.ToList();
        var numCols = Math.Min(5, playersList.Count);
        var numRows = (playersList.Count + numCols - 1) / numCols;

        for (var row = 0; row < numRows; row++)
        {
            html.Append("<tr>");
            for (var col = 0; col < numCols; col++)
            {
                var idx = row * numCols + col;
                if (idx < playersList.Count)
                {
                    var (name, pokemon) = playersList[idx];
                    var color = name.ToColorHexCodeWithCustoms();
                    html.Append("<td style=\"text-align: center; padding: 5px; width: 100px;\">");
                    html.Append($"<img src=\"{PokeRaceConstants.RACE_POKEMON[pokemon].MiniSprite}\" width=\"40\" height=\"40\"><br>");
                    html.Append($"<b style=\"color: {color}\">{name}</b><br>");
                    html.Append($"<span>{pokemon}</span>");
                    html.Append("</td>");
                }
                else
                {
                    html.Append("<td style=\"width: 100px;\"></td>");
                }
            }
            html.Append("</tr>");
        }
        html.Append("</table>");

        html.Append("<p style=\"text-align: center;\">La course va commencer dans 3 secondes...</p>");
        html.Append(CLOSING_DIV);
        return html.ToString();
    }

    private string BuildRaceUpdateHtml()
    {
        var html = new System.Text.StringBuilder();
        html.Append(RACE_OUTER_DIV);
        html.Append($"<h3 style=\"text-align: center; margin: 0;\">🏁 Course de Pokémon - Tour {_turn} 🏁</h3>");
        html.Append("<div style=\"margin: 10px 0;\">");

        var sortedPositions = _positions.OrderByDescending(kvp => kvp.Value).ToList();
        foreach (var (pokemon, position) in sortedPositions)
        {
            var playerName = _players.Values.FirstOrDefault(player => player.Pokemon == pokemon).Name ?? "???";
            var color = playerName.ToColorHexCodeWithCustoms();
            var progress = Math.Min(100, position / PokeRaceConstants.RACE_LENGTH * 100);

            html.Append($"<div style=\"margin: 5px 0;\"><b style=\"color: {color}\">{playerName}</b> - {pokemon}");
            html.Append("<div style=\"background-color: #555; height: 20px; border-radius: 10px; margin-top: 2px;\">");
            html.Append($"<div style=\"background-color: #f0f0f0; height: 20px; border-radius: 10px; width: {progress:F1}%; position: relative;\">");
            html.Append($"<img src=\"{PokeRaceConstants.RACE_POKEMON[pokemon].MiniSprite}\" style=\"position: absolute; right: 0; top: -15px;\" width=\"40\" height=\"40\">");
            html.Append("</div></div></div>");
        }

        html.Append(CLOSING_DIV);

        if (_allEvents.Count > 0)
        {
            html.Append("<div style=\"margin-top: 10px; text-align: center;\"><b>Événements récents:</b><br>");
            foreach (var evt in _allEvents)
                html.Append($"{evt}<br>");
            html.Append(CLOSING_DIV);
        }

        if (_finished.Count > 0)
        {
            html.Append("<div style=\"margin-top: 10px; text-align: center;\"><b>Arrivées:</b><br>");
            for (var i = 0; i < _finished.Count; i++)
            {
                var pokemon = _finished[i];
                var playerName = _players.Values.FirstOrDefault(player => player.Pokemon == pokemon).Name ?? "???";
                var color = playerName.ToColorHexCodeWithCustoms();
                html.Append($"#{i + 1} - <b style=\"color: {color}\">{playerName}</b> avec {pokemon}<br>");
            }
            html.Append(CLOSING_DIV);
        }

        html.Append(CLOSING_DIV);
        return html.ToString();
    }

    private string BuildRaceResultsHtml()
    {
        var podiumColors = new[] { "#C0C0C0", "#FFD700", "#CD7F32" };
        var podiumHeights = new[] { "80px", "110px", "60px" };
        var podiumOrder = new[] { 1, 0, 2 };

        var html = new System.Text.StringBuilder();
        html.Append(RACE_OUTER_DIV);
        html.Append("<h3 style=\"text-align: center; margin: 0;\">🏆 Résultats de la Course 🏆</h3>");
        html.Append("<div style=\"display: flex; justify-content: center; align-items: flex-end; margin: 15px 0; padding-top: 50px;\">");

        for (var i = 0; i < podiumOrder.Length; i++)
        {
            var pos = podiumOrder[i];
            var podiumColor = podiumColors[i];
            var podiumHeight = podiumHeights[i];

            if (pos < _finished.Count)
            {
                var pokemon = _finished[pos];
                var playerName = _players.Values.FirstOrDefault(player => player.Pokemon == pokemon).Name ?? "???";
                var nameColor = playerName.ToColorHexCodeWithCustoms();
                var rank = pos + 1;

                html.Append("<div style=\"text-align: center; margin: 0 15px; position: relative;\">");
                html.Append("<div style=\"position: absolute; top: -45px; left: 50%; transform: translateX(-50%);\">");
                html.Append($"<img src=\"{PokeRaceConstants.RACE_POKEMON[pokemon].MiniSprite}\" width=\"60\" height=\"60\" style=\"filter: drop-shadow(0 0 3px {podiumColor});\"></div>");
                html.Append($"<div style=\"background: linear-gradient(to bottom, {podiumColor}, {podiumColor}CC); height: {podiumHeight}; width: 90px; border-radius: 5px 5px 0 0; box-shadow: 0 4px 8px rgba(0,0,0,0.3);\">");
                html.Append($"<div style=\"position: relative; top: 50%; transform: translateY(-50%); font-weight: bold; font-size: 24px; text-shadow: 0 0 3px #000;\">#{rank}</div></div>");
                html.Append("<div style=\"background-color: #444; padding: 8px; border-radius: 0 0 5px 5px;\">");
                html.Append($"<b style=\"color: {nameColor}\">{playerName}</b><br><span style=\"color: #DDD;\">{pokemon}</span></div>");
                html.Append(CLOSING_DIV);
            }
            else
            {
                html.Append($"<div style=\"text-align: center; margin: 0 15px;\">");
                html.Append($"<div style=\"background: linear-gradient(to bottom, {podiumColor}88, {podiumColor}44); height: {podiumHeight}; width: 90px; border-radius: 5px 5px 0 0;\">");
                html.Append($"<div style=\"position: relative; top: 50%; transform: translateY(-50%); font-weight: bold; font-size: 24px;\">#{pos + 1}</div></div>");
                html.Append("<div style=\"background-color: #444; padding: 8px; border-radius: 0 0 5px 5px;\"><span style=\"color: #888;\">Vide</span></div></div>");
            }
        }

        html.Append(CLOSING_DIV);
        html.Append("<div style=\"margin-top: 15px;\"><h4 style=\"text-align: center; margin: 5px 0;\">Classement complet</h4>");

        for (var i = 0; i < _finished.Count; i++)
        {
            var pokemon = _finished[i];
            var playerName = _players.Values.FirstOrDefault(player => player.Pokemon == pokemon).Name ?? "???";
            var nameColor = playerName.ToColorHexCodeWithCustoms();
            html.Append($"<div style=\"margin: 3px 0; text-align: center;\">");
            html.Append($"<b>#{i + 1}</b> - <b style=\"color: {nameColor}\">{playerName}</b> avec {pokemon} ({PokeRaceConstants.RACE_POKEMON[pokemon].Type})</div>");
        }

        html.Append(CLOSING_DIV);
        html.Append("<p style=\"text-align: center; margin-top: 15px;\">Merci d'avoir participé à la course! Tapez <b>-pokerace</b> pour commencer une nouvelle course.</p>");
        html.Append(CLOSING_DIV);
        return html.ToString();
    }
}