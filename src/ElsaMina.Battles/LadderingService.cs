using ElsaMina.Core;

namespace ElsaMina.Battles;

public class LadderingService : ILadderingService
{
    private readonly IBot _bot;
    private readonly IBattleTeamsService _battleTeamsService;
    private readonly Lock _lock = new();

    private string _format;

    public LadderingService(IBot bot, IBattleTeamsService battleTeamsService)
    {
        _bot = bot;
        _battleTeamsService = battleTeamsService;

        _battleTeamsService.TeamChanged += HandleTeamChanged;
    }

    private void HandleTeamChanged(object sender, string team)
    {
        if (!string.IsNullOrEmpty(team))
        {
            _bot.Send($"|/utm {team}");
        }
    }

    public bool IsLaddering
    {
        get
        {
            lock (_lock)
            {
                return _format != null;
            }
        }
    }

    public string Format
    {
        get
        {
            lock (_lock)
            {
                return _format;
            }
        }
    }

    public void Start(string format)
    {
        lock (_lock)
        {
            _format = format;
            Search();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _format = null;
        }
    }

    public void OnBattleEnded()
    {
        lock (_lock)
        {
            if (_format != null)
            {
                Search();
            }
        }
    }

    private void Search()
    {
        _bot.Say(string.Empty, $"/search {_format}");
    }
}