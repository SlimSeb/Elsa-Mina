using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, BattleContext> _contexts = new();

    public BattleService(IBattleMessageParser messageParser,
        IBattleDecisionService decisionService,
        IBot bot,
        IConfiguration configuration)
    {
        _messageParser = messageParser;
        _decisionService = decisionService;
        _bot = bot;
        _configuration = configuration;
    }

    public Task HandleMessageAsync(string[] parts, string roomId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !roomId.StartsWith("battle-"))
        {
            return Task.CompletedTask;
        }

        var context = _contexts.GetOrAdd(roomId, id => new BattleContext(id));
        if (!_messageParser.TryApplyMessage(parts, roomId, context, out var result))
        {
            return Task.CompletedTask;
        }

        if (result.Type == BattleMessageType.BattleStarted && !context.HasAnnouncedStart)
        {
            _bot.Say(roomId, "/timer on");
            _bot.Say(roomId, "Battle started. Good luck!");
            context.HasAnnouncedStart = true;
        }

        if (result.Type == BattleMessageType.BattleEnded)
        {
            if (!context.HasAnnouncedEnd)
            {
                AnnounceBattleEnd(roomId, result);
                context.HasAnnouncedEnd = true;
            }

            _bot.Say(roomId, "/leave");
            _contexts.TryRemove(roomId, out _);
            return Task.CompletedTask;
        }

        if (result.Type == BattleMessageType.RequestUpdated &&
            _decisionService.TryGetDecision(context, out var decision))
        {
            var command = BuildCommand(decision, context.Rqid);
            if (!string.IsNullOrWhiteSpace(command))
            {
                _bot.Say(roomId, command);
            }
        }

        return Task.CompletedTask;
    }

    private void AnnounceBattleEnd(string roomId, BattleMessageResult result)
    {
        if (result.IsTie)
        {
            _bot.Say(roomId, "Battle ended in a tie. GG!");
            return;
        }

        if (string.IsNullOrWhiteSpace(result.WinnerName))
        {
            _bot.Say(roomId, "Battle ended. GG!");
            return;
        }

        var winnerId = result.WinnerName.ToLowerAlphaNum();
        var botId = _configuration.Name.ToLowerAlphaNum();
        var message = winnerId == botId ? "GG! I won." : "GG! I lost.";
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
