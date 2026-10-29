using System.Collections.Concurrent;
using System.Text.Json;
using ElsaMina.Core;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Teams;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Battles.Challenges;

/// <summary>
/// Accepts any user's challenge in any format, with:
/// <list type="bullet">
/// <item>no team when the format generates them (random battles);</item>
/// <item>else the team chosen for the format with -useteam;</item>
/// <item>else a team saved for the format (-add-team), picked at random among those the server validates (/vtm).</item>
/// </list>
/// Without any, the challenge is rejected and the challenger told why.
/// Showdown counts each validation and battle of an IP against a limit of 12 every 3 minutes (see
/// <see cref="LadderingService"/>): a challenge validates <see cref="MAX_TEAMS_TRIED"/> teams at most, and the server's
/// answers are remembered until the bot restarts. Challenges are answered one at a time: the server keeps one team per
/// user, which validating and accepting both set.
/// </summary>
public sealed class ChallengeService : IChallengeService, IDisposable
{
    public const int MAX_TEAMS_TRIED = 3;
    private const string CUSTOM_RULES_SEPARATOR = "@@@";

    private readonly IBot _bot;
    private readonly IBattleTeamSender _teamSender;
    private readonly IBattleTeamsService _battleTeamsService;
    private readonly IFormatsManager _formatsManager;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITeamValidator _teamValidator;
    private readonly IRandomService _randomService;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    // Challengers waiting for an answer: the server sends a challenge again when the bot reconnects
    private readonly ConcurrentDictionary<string, byte> _pendingChallengers = new();
    // Only used while answering, one challenge at a time
    private readonly Dictionary<(string Format, string PackedTeam), bool> _validations = new();

    public ChallengeService(IBot bot, IBattleTeamSender teamSender, IBattleTeamsService battleTeamsService,
        IFormatsManager formatsManager, IBotDbContextFactory dbContextFactory, ITeamValidator teamValidator,
        IRandomService randomService)
    {
        _bot = bot;
        _teamSender = teamSender;
        _battleTeamsService = battleTeamsService;
        _formatsManager = formatsManager;
        _dbContextFactory = dbContextFactory;
        _teamValidator = teamValidator;
        _randomService = randomService;
    }

    public async Task HandleChallengeAsync(string challenger, string format,
        CancellationToken cancellationToken = default)
    {
        if (!_pendingChallengers.TryAdd(challenger, 0))
        {
            return;
        }

        try
        {
            await _oneAtATime.WaitAsync(cancellationToken);
            try
            {
                await AnswerAsync(challenger, format, cancellationToken);
            }
            finally
            {
                _oneAtATime.Release();
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not answer {Challenger}'s challenge in {Format}", challenger, format);
        }
        finally
        {
            _pendingChallengers.TryRemove(challenger, out _);
        }
    }

    private async Task AnswerAsync(string challenger, string format, CancellationToken cancellationToken)
    {
        var accept = $"/accept {challenger}";
        if (_formatsManager.HasGeneratedTeams(format))
        {
            _teamSender.SendWithTeam(null, accept);
            Log.Information("Accepted {Challenger}'s challenge in {Format}", challenger, format);
            return;
        }

        var formatId = format.Split(CUSTOM_RULES_SEPARATOR)[0].ToLowerAlphaNum();
        var chosenTeam = _battleTeamsService.GetTeam(formatId);
        if (!string.IsNullOrEmpty(chosenTeam))
        {
            _teamSender.SendWithTeam(chosenTeam, accept);
            Log.Information("Accepted {Challenger}'s challenge in {Format} with the team chosen for it", challenger,
                format);
            return;
        }

        var (team, packedTeam, allChecked) = await FindValidTeamAsync(format, formatId, cancellationToken);
        if (team != null)
        {
            _teamSender.SendWithTeam(packedTeam, accept);
            Log.Information("Accepted {Challenger}'s challenge in {Format} with the team {Team}", challenger, format,
                team.Name);
            return;
        }

        var formatName = _formatsManager.GetCleanFormat(formatId);
        _bot.Send($"|/reject {challenger}");
        _bot.Send(allChecked
            ? $"|/pm {challenger}, Sorry, I have no valid team for {formatName}."
            : $"|/pm {challenger}, Sorry, I could not check all my teams for {formatName}, please try again later.");
        Log.Information("Rejected {Challenger}'s challenge in {Format}: no valid team found (all checked: {AllChecked})",
            challenger, format, allChecked);
    }

    /// <summary>
    /// A random team saved for the format that the server validates, if any, and whether every team was checked
    /// (when not, the next challenge tries the others).
    /// </summary>
    private async Task<(Team Team, string PackedTeam, bool AllChecked)> FindValidTeamAsync(string format,
        string formatId, CancellationToken cancellationToken)
    {
        List<Team> teams;
        await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            teams = await dbContext.Teams.Where(team => team.Format == formatId).ToListAsync(cancellationToken);
        }

        _randomService.ShuffleInPlace(teams);
        var validationFormat = format.ToLowerInvariant();
        var validated = 0;
        foreach (var team in teams)
        {
            var packedTeam = Pack(team);
            if (packedTeam == null)
            {
                continue;
            }

            if (_validations.TryGetValue((validationFormat, packedTeam), out var isKnownValid))
            {
                if (isKnownValid)
                {
                    return (team, packedTeam, true);
                }

                continue;
            }

            if (validated == MAX_TEAMS_TRIED)
            {
                return (null, null, false);
            }

            validated++;
            var isValid = await _teamValidator.ValidateAsync(packedTeam, format, cancellationToken);
            if (isValid == null)
            {
                // Rate limited or no answer: the next validations would fail the same way
                return (null, null, false);
            }

            _validations[(validationFormat, packedTeam)] = isValid.Value;
            if (isValid.Value)
            {
                return (team, packedTeam, true);
            }

            Log.Information("Team {Team} is not valid in {Format}", team.Name, format);
        }

        return (null, null, true);
    }

    private static string Pack(Team team)
    {
        try
        {
            var sets = JsonSerializer.Deserialize<List<PokemonSet>>(team.TeamJson);
            var packedTeam = sets == null ? null : ShowdownTeamsUtils.PackTeam(sets);
            return string.IsNullOrEmpty(packedTeam) ? null : packedTeam;
        }
        catch (JsonException exception)
        {
            Log.Warning(exception, "Team {Team} could not be read", team.Name);
            return null;
        }
    }

    public void Dispose() => _oneAtATime.Dispose();
}
