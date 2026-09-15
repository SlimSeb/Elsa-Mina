using System.Text.RegularExpressions;
using ElsaMina.Logging;
using System.Text.Json;

namespace ElsaMina.Battles.Strategies.Llm;

public partial class LlmBattleDecisionParser : ILlmBattleDecisionParser
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"\bMOVE\s+(?<index>[1-4])(?:\s+(?<tera>TERA|TERASTALLIZE))?\b", RegexOptions.IgnoreCase)]
    private static partial Regex MoveRegex();

    [GeneratedRegex(@"\bSWITCH\s+(?<index>[1-6])\b", RegexOptions.IgnoreCase)]
    private static partial Regex SwitchRegex();

    [GeneratedRegex(@"\b(?:TEAM|LEAD)\s+(?<index>[1-6])\b", RegexOptions.IgnoreCase)]
    private static partial Regex TeamPreviewRegex();

    public LlmDecisionParsedResult Parse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return LlmDecisionParsedResult.Invalid("Empty response");
        }

        // 1. Try parsing JSON
        var jsonResult = ParseJsonDecision(response);
        if (jsonResult != null)
        {
            return jsonResult;
        }

        // 2. Fallback regex parsing on plain text
        return ParseTextDecision(response)
               ?? LlmDecisionParsedResult.Invalid("No recognizable decision format found in response");
    }

    private static LlmDecisionParsedResult ParseJsonDecision(string response)
    {
        var jsonCandidate = ExtractJson(response);
        if (string.IsNullOrWhiteSpace(jsonCandidate))
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<LlmDecisionDto>(jsonCandidate, JSON_OPTIONS);
            if (dto == null || dto.Index <= 0)
            {
                return null;
            }

            return BuildJsonResult(dto);
        }
        catch (Exception exception)
        {
            Log.Debug("Failed to parse JSON from LLM battle response: {Message}", exception.Message);
            return null;
        }
    }

    private static LlmDecisionParsedResult BuildJsonResult(LlmDecisionDto dto)
    {
        return dto.Decision?.Trim().ToLowerInvariant() switch
        {
            "move" => LlmDecisionParsedResult.Valid(
                BattleDecisionType.Move,
                dto.Index,
                dto.Terastallize,
                dto.Reasoning ?? ""),
            "switch" => LlmDecisionParsedResult.Valid(
                BattleDecisionType.Switch,
                dto.Index,
                terastallize: false,
                dto.Reasoning ?? ""),
            "teampreview" or "team" or "lead" => LlmDecisionParsedResult.Valid(
                BattleDecisionType.TeamPreview,
                dto.Index,
                terastallize: false,
                dto.Reasoning ?? ""),
            _ => null
        };
    }

    private static LlmDecisionParsedResult ParseTextDecision(string response)
    {
        var moveMatch = MoveRegex().Match(response);
        if (moveMatch.Success && int.TryParse(moveMatch.Groups["index"].Value, out var moveIndex))
        {
            var isTera = moveMatch.Groups["tera"].Success;
            return LlmDecisionParsedResult.Valid(BattleDecisionType.Move, moveIndex, isTera);
        }

        var switchMatch = SwitchRegex().Match(response);
        if (switchMatch.Success && int.TryParse(switchMatch.Groups["index"].Value, out var switchIndex))
        {
            return LlmDecisionParsedResult.Valid(BattleDecisionType.Switch, switchIndex);
        }

        var teamMatch = TeamPreviewRegex().Match(response);
        if (teamMatch.Success && int.TryParse(teamMatch.Groups["index"].Value, out var teamIndex))
        {
            return LlmDecisionParsedResult.Valid(BattleDecisionType.TeamPreview, teamIndex);
        }

        return null;
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
