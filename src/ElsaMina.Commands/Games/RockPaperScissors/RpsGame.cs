using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.RockPaperScissors;

public class RpsGame : Game, IRpsGame
{
    private static int _nextGameId = 1;

    private readonly IConfiguration _configuration;
    private readonly ISystemService _systemService;
    private readonly ITemplatesManager _templatesManager;
    private readonly List<string> _players = [];
    private readonly Dictionary<string, RpsChoice> _choices = [];

    [UsedImplicitly]
    public RpsGame(IConfiguration configuration, ISystemService systemService, ITemplatesManager templatesManager)
    {
        _configuration = configuration;
        _systemService = systemService;
        _templatesManager = templatesManager;
        GameId = _nextGameId++;
    }

    private int GameId { get; }
    public IContext Context { get; set; }
    public IReadOnlyList<string> Players => _players;
    public override string Identifier => nameof(RpsGame);

    private string HtmlId => $"rps-{GameId}";

    public async Task<(bool Success, string MessageKey, object[] Args)> Join(string userName)
    {
        if (_players.Count >= 2)
            return (false, "rps_game_full", []);

        if (_players.Any(player => player.ToLowerAlphaNum() == userName.ToLowerAlphaNum()))
            return (false, "rps_already_joined", []);

        _players.Add(userName);

        if (_players.Count == 2)
        {
            OnStart();
            await ShowChoicePanel();
        }
        else
        {
            await ShowLobby();
        }

        return (true, "rps_join_success", [userName]);
    }

    public async Task Play(string userId, RpsChoice choice)
    {
        if (!IsStarted || IsEnded)
            return;

        var playerName = _players.FirstOrDefault(p => p.ToLowerAlphaNum() == userId);
        if (playerName is null)
            return;

        if (!_choices.TryAdd(playerName.ToLowerAlphaNum(), choice))
            return;

        if (_choices.Count == 2)
        {
            await _systemService.SleepAsync(TimeSpan.FromMilliseconds(500));
            await ShowResult();
        }
    }

    public void Cancel()
    {
        Context.SendUpdatableHtml(HtmlId, string.Empty, true);
        OnEnd();
    }

    private async Task ShowLobby()
    {
        var html = await _templatesManager.GetTemplateAsync("Games/RockPaperScissors/RpsLobby",
            new RpsViewModel
            {
                Culture = Context.Culture,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = Context.RoomId,
                Players = _players,
                Choices = _choices,
                WaitingCount = 2 - _players.Count
            });
        Context.SendUpdatableHtml(HtmlId, html.RemoveNewlines(), _players.Count > 1);
    }

    private async Task ShowChoicePanel()
    {
        var html = await _templatesManager.GetTemplateAsync("Games/RockPaperScissors/RpsChoicePanel",
            new RpsViewModel
            {
                Culture = Context.Culture,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = Context.RoomId,
                Players = _players,
                Choices = _choices,
                WaitingCount = 0
            });
        Context.SendUpdatableHtml(HtmlId, html.RemoveNewlines(), true);
    }

    private async Task ShowResult()
    {
        var html = await _templatesManager.GetTemplateAsync("Games/RockPaperScissors/RpsResult",
            new RpsViewModel
            {
                Culture = Context.Culture,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = Context.RoomId,
                Players = _players,
                Choices = _choices,
                WaitingCount = 0
            });
        Context.SendUpdatableHtml(HtmlId, html.RemoveNewlines(), true);
        OnEnd();
    }

    public static string ChoiceEmoji(RpsChoice choice) => choice switch
    {
        RpsChoice.Rock => "✊",
        RpsChoice.Paper => "📄",
        RpsChoice.Scissors => "✂️",
        _ => "?"
    };

    public static bool Beats(RpsChoice attacker, RpsChoice defender) =>
        (attacker == RpsChoice.Rock && defender == RpsChoice.Scissors) ||
        (attacker == RpsChoice.Scissors && defender == RpsChoice.Paper) ||
        (attacker == RpsChoice.Paper && defender == RpsChoice.Rock);
}
