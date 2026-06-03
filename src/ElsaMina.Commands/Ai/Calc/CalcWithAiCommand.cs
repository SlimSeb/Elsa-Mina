using System.Net;
using ElsaMina.Commands.Ai.LanguageModel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;
using Newtonsoft.Json;

namespace ElsaMina.Commands.Ai.Calc;

[NamedCommand("calc-ai", "calcai", "aicalc")]
public class CalcWithAiCommand : Command
{
    private readonly ILanguageModelProvider _languageModelProvider;
    private readonly IResourcesService _resourcesService;
    private readonly IDamageCalculator _damageCalculator;

    public CalcWithAiCommand(ILanguageModelProvider languageModelProvider,
        IResourcesService resourcesService,
        IDamageCalculator damageCalculator)
    {
        _languageModelProvider = languageModelProvider;
        _resourcesService = resourcesService;
        _damageCalculator = damageCalculator;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "calc_ai_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var input = context.Target?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            context.ReplyLocalizedMessage("calc_ai_usage");
            return;
        }

        var request = new LanguageModelRequest
        {
            SystemPrompt = _resourcesService.GetString("calc_ai_prompt", context.Culture),
            InputConversation =
            [
                new LanguageModelMessage { Role = MessageRole.User, Content = input }
            ]
        };

        var response = await _languageModelProvider.AskLanguageModelAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(response))
        {
            context.ReplyLocalizedMessage("calc_ai_error");
            return;
        }

        CalcRequestDto calcRequest;
        try
        {
            calcRequest = JsonConvert.DeserializeObject<CalcRequestDto>(ExtractJson(response));
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to parse calc request from AI response: {Response}", response);
            context.ReplyLocalizedMessage("calc_ai_error");
            return;
        }

        if (calcRequest == null
            || !string.IsNullOrWhiteSpace(calcRequest.Error)
            || calcRequest.Attacker == null
            || calcRequest.Defender == null
            || string.IsNullOrWhiteSpace(calcRequest.Move))
        {
            context.ReplyLocalizedMessage("calc_ai_error");
            return;
        }

        string description;
        try
        {
            description = _damageCalculator.Calculate(calcRequest);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Damage calculation failed for AI response: {Response}", response);
            context.ReplyLocalizedMessage("calc_ai_error");
            return;
        }

        context.ReplyHtml(FormatDescription(description));
    }

    /// <summary>
    /// Pulls the JSON object out of the model response, tolerating markdown fences or stray prose.
    /// </summary>
    private static string ExtractJson(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        return start >= 0 && end > start ? response.Substring(start, end - start + 1) : response;
    }

    /// <summary>
    /// Renders the calc description as the Smogon-style two-line comment, splitting the matchup
    /// from the damage roll at the first <c>": "</c> separator.
    /// </summary>
    private static string FormatDescription(string description)
    {
        var separatorIndex = description.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return $"<code>// {WebUtility.HtmlEncode(description)}</code>";
        }

        var matchup = WebUtility.HtmlEncode(description[..separatorIndex]);
        var damage = WebUtility.HtmlEncode(description[(separatorIndex + 2)..]);
        return $"<code>// {matchup}:<br>//&nbsp;&nbsp;{damage}</code>";
    }
}
