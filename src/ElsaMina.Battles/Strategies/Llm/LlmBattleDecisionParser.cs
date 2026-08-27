using System.Text.RegularExpressions;
using ElsaMina.Logging;
using Newtonsoft.Json;

namespace ElsaMina.Battles.Strategies.Llm;

public class LlmBattleDecisionParser : ILlmBattleDecisionParser
{
    private static readonly Regex MoveRegex = new(
        @"\bMOVE\s+(?<index>[1-4])(?:\s+(?<tera>TERA|TERASTALLIZE))?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SwitchRegex = new(
        @"\bSWITCH\s+(?<index>[1-6])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TeamPreviewRegex = new(
        @"\b(?:TEAM|LEAD)\s+(?<index>[1-6])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public LlmDecisionParsedResult Parse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return LlmDecisionParsedResult.Invalid("Empty response");
        }

        // 1. Try parsing JSON
        var jsonCandidate = ExtractJson(response);
        if (!string.IsNullOrWhiteSpace(jsonCandidate))
        {
            try
            {
                var dto = JsonConvert.DeserializeObject<LlmDecisionDto>(jsonCandidate);
                if (dto != null && dto.Index > 0)
                {
                    var decisionStr = dto.Decision?.Trim().ToLowerInvariant();
                    switch (decisionStr)
                    {
                        case "move":
                            return LlmDecisionParsedResult.Valid(
                                BattleDecisionType.Move,
                                dto.Index,
                                dto.Terastallize,
                                dto.Reasoning ?? "");
                        case "switch":
                            return LlmDecisionParsedResult.Valid(
                                BattleDecisionType.Switch,
                                dto.Index,
                                terastallize: false,
                                dto.Reasoning ?? "");
                        case "teampreview" or "team" or "lead":
                            return LlmDecisionParsedResult.Valid(
                                BattleDecisionType.TeamPreview,
                                dto.Index,
                                terastallize: false,
                                dto.Reasoning ?? "");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Failed to parse JSON from LLM battle response: {Message}", ex.Message);
            }
        }

        // 2. Fallback regex parsing on plain text
        var moveMatch = MoveRegex.Match(response);
        if (moveMatch.Success && int.TryParse(moveMatch.Groups["index"].Value, out var moveIndex))
        {
            var isTera = moveMatch.Groups["tera"].Success;
            return LlmDecisionParsedResult.Valid(BattleDecisionType.Move, moveIndex, isTera);
        }

        var switchMatch = SwitchRegex.Match(response);
        if (switchMatch.Success && int.TryParse(switchMatch.Groups["index"].Value, out var switchIndex))
        {
            return LlmDecisionParsedResult.Valid(BattleDecisionType.Switch, switchIndex);
        }

        var teamMatch = TeamPreviewRegex.Match(response);
        if (teamMatch.Success && int.TryParse(teamMatch.Groups["index"].Value, out var teamIndex))
        {
            return LlmDecisionParsedResult.Valid(BattleDecisionType.TeamPreview, teamIndex);
        }

        return LlmDecisionParsedResult.Invalid("No recognizable decision format found in response");
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return text[start..(end + 1)];
        }

        return null;
    }
}
