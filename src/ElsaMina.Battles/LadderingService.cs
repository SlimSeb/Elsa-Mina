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
        var packedTeam = _battleTeamsService.GetTeam(_format);
        if (!string.IsNullOrEmpty(packedTeam))
        {
            _bot.Say(string.Empty, $"/utm {packedTeam}");
        }

        _bot.Say(string.Empty, $"/search {_format}");
    }
}
