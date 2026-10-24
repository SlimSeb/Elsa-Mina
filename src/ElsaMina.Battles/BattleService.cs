using System.Collections.Concurrent;
using ElsaMina.Battles.Replays;
using ElsaMina.Battles.Results;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles;

public class BattleService : IBattleService
{
    private readonly IBattleMessageParser _messageParser;
    private readonly IBattleDecisionService _decisionService;
    private readonly IBot _bot;
    private readonly IConfiguration _configuration;
    private readonly ILadderingService _ladderingService;
    private readonly IBattleResultsService _battleResultsService;
    private readonly IReplaySavingService _replaySavingService;
    private readonly ConcurrentDictionary<string, BattleContext> _contexts = new();

    public BattleService(IBattleMessageParser messageParser,
        IBattleDecisionService decisionService,
        IBot bot,
        IConfiguration configuration,
        ILadderingService ladderingService,
        IBattleResultsService battleResultsService,
        IReplaySavingService replaySavingService)
    {
        _messageParser = messageParser;
        _decisionService = decisionService;
        _bot = bot;
        _configuration = configuration;
        _ladderingService = ladderingService;
        _battleResultsService = battleResultsService;
        _replaySavingService = replaySavingService;
    }

    public IReadOnlyList<BattleContext> GetBattlesInProgress() => _contexts.Values
        .Where(context => !string.IsNullOrEmpty(context.SideId) && !context.IsBattleOver)
        .OrderBy(context => context.RoomId, StringComparer.Ordinal)
        .ToList();

    public async Task HandleMessageAsync(string[] parts, string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("battle-"))
        {
            return;
        }

        var context = _contexts.GetOrAdd(roomId, id => new BattleContext(id));
        context.Lines.Add(string.Join("|", parts));
        if (!_messageParser.TryApplyMessage(parts, roomId, context, out var result))
        {
            return;
        }

        if (result.Type == BattleMessageType.BattleStarted && !context.HasAnnouncedStart)
        {
            _bot.Say(roomId, "/timer on");
            _bot.Say(roomId, "Battle started. Good luck!");
            context.HasAnnouncedStart = true;
            _battleResultsService.OnBattleStarted(context);
        }

        if (result.Type == BattleMessageType.BattleEnded)
        {
            // Spectated battles end too (and replay their |win| when joined late), only ours count
            var isOurBattle = !string.IsNullOrEmpty(context.SideId);
            var outcome = GetOutcome(result);
            if (!context.HasAnnouncedEnd)
            {
                AnnounceBattleEnd(roomId, outcome);
                context.HasAnnouncedEnd = true;
            }

            if (isOurBattle)
            {
                if (outcome != null)
                {
                    _battleResultsService.OnBattleEnded(context, outcome.Value);
                }

                // Its /savereplay goes to the battle room, so before /leave
                await _replaySavingService.OnBattleEndedAsync(context, cancellationToken);
            }

            _bot.Say(roomId, "/leave");
            _contexts.TryRemove(roomId, out _);
            if (isOurBattle)
            {
                _ladderingService.OnBattleEnded();
            }

            return;
        }

        if (result.Type == BattleMessageType.RequestUpdated)
        {
            var decision = await _decisionService.GetDecisionAsync(context, cancellationToken);
            if (decision == null)
            {
                return;
            }

            var command = BuildCommand(decision, context.Rqid);
            if (!string.IsNullOrWhiteSpace(command))
            {
                _bot.Say(roomId, command);
            }
        }
    }

    /// <summary>
    /// Null when the server did not name the winner.
    /// </summary>
    private BattleOutcome? GetOutcome(BattleMessageResult result)
    {
        if (result.IsTie)
        {
            return BattleOutcome.Tied;
        }

        if (string.IsNullOrWhiteSpace(result.WinnerName))
        {
            return null;
        }

        return result.WinnerName.ToLowerAlphaNum() == _configuration.Name.ToLowerAlphaNum()
            ? BattleOutcome.Won
            : BattleOutcome.Lost;
    }

    private void AnnounceBattleEnd(string roomId, BattleOutcome? outcome)
    {
        var message = outcome switch
        {
            BattleOutcome.Tied => "Battle ended in a tie. GG!",
            BattleOutcome.Won => "GG! I won.",
            BattleOutcome.Lost => "GG! I lost.",
            _ => "Battle ended. GG!"
        };
        _bot.Say(roomId, message);
    }

    private static string BuildCommand(BattleDecision decision, int rqid)
    {
        var command = decision.Type switch
        {
            BattleDecisionType.TeamPreview => $"/team {decision.Choices[0]}",
            BattleDecisionType.Switch => $"/choose {string.Join(", ", decision.Choices.Select(index => $"switch {index}"))}",
            BattleDecisionType.Move => $"/choose {string.Join(", ", decision.Choices.Select(index => decision.UseTerastallize ? $"move {index} terastallize" : $"move {index}"))}",
            _ => string.Empty
        };

        return string.IsNullOrEmpty(command) ? command : $"{command}|{rqid}";
    }
}
