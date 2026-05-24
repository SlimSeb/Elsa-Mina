using ElsaMina.Core.Utils;
using Newtonsoft.Json;

namespace ElsaMina.Commands.Tournaments;

public static class TournamentHelper
{
    private const string FINISHED_STATE = "finished";
    private const string SINGLE_ELIMINATION_ID = "singleelimination";
    private const string ROUND_ROBIN_ID = "roundrobin";

    private static Dictionary<string, int> ParseTourTree(TournamentNode node)
    {
        var teamScores = new Dictionary<string, int>();
        if (node.Team != null)
        {
            teamScores[node.Team] = 0;
        }

        if (!string.IsNullOrEmpty(node.State) && node.State == FINISHED_STATE && node.Team != null)
        {
            teamScores[node.Team]++;
        }

        foreach (var childNode in node.Children ?? Enumerable.Empty<TournamentNode>())
        {
            var childScores = ParseTourTree(childNode);
            foreach (var (team, score) in childScores)
            {
                teamScores[team] = teamScores.GetValueOrDefault(team, 0) + score;
            }
        }

        return teamScores;
    }

    public static TournamentResults ParseTourResults(string jsonData)
    {
        var data = JsonConvert.DeserializeObject<TournamentData>(jsonData);
        if (IsSingleElimination(data))
        {
            return ParseSingleEliminationResults(data);
        }
        if (IsRoundRobin(data))
        {
            return ParseRoundRobinResults(data);
        }
        return null;
    }

    private static TournamentResults ParseSingleEliminationResults(TournamentData data)
    {
        var teamScores = ParseTourTree(data.BracketData.RootNode);
        var tournamentResults = CreateTournamentResults(data, teamScores);

        PopulateFinalistsAndSemiFinalists(data.BracketData.RootNode, tournamentResults);
        PopulateFormat(data, tournamentResults);

        return tournamentResults;
    }

    private static TournamentResults ParseRoundRobinResults(TournamentData data)
    {
        var players = data.BracketData.TableHeaders.Cols;
        var scores = data.BracketData.Scores;

        var winsCount = players
            .Select((player, index) => (player, score: index < scores.Count ? scores[index] : 0))
            .ToDictionary(
                x => x.player.ToLowerAlphaNum(),
                x => x.score);

        return new TournamentResults
        {
            Players = players.ToList(),
            WinsCount = winsCount,
            Winner = data.Results[0][0].ToLowerAlphaNum(),
            RunnerUp = data.Results.Count > 1 ? data.Results[1][0].ToLowerAlphaNum() : string.Empty,
            SemiFinalists = data.Results.Skip(2).Select(rank => rank[0].ToLowerAlphaNum()).ToList(),
            Format = data.Format
        };
    }

    private static void PopulateFormat(TournamentData data, TournamentResults tournamentResults)
    {
        tournamentResults.Format = data?.Format;
    }

    private static bool IsSingleElimination(TournamentData data) =>
        data.Generator.ToLowerAlphaNum() == SINGLE_ELIMINATION_ID;

    private static bool IsRoundRobin(TournamentData data) =>
        data.Generator.ToLowerAlphaNum() == ROUND_ROBIN_ID;

    private static TournamentResults CreateTournamentResults(TournamentData data, Dictionary<string, int> teamScores)
    {
        return new TournamentResults
        {
            Players = teamScores.Keys.ToList(),
            WinsCount = teamScores.ToDictionary(
                kvp => kvp.Key.ToLowerAlphaNum(),
                kvp => kvp.Value),
            Winner = data.Results[0][0].ToLowerAlphaNum(),
            RunnerUp = string.Empty,
            SemiFinalists = []
        };
    }

    private static void PopulateFinalistsAndSemiFinalists(TournamentNode rootNode, TournamentResults results)
    {
        foreach (var childNode in rootNode.Children ?? Enumerable.Empty<TournamentNode>())
        {
            UpdateFinalist(childNode, results);
            AddSemiFinalists(childNode, results);
        }
    }

    private static void UpdateFinalist(TournamentNode node, TournamentResults results)
    {
        var team = node.Team?.ToLowerAlphaNum();
        if (!string.IsNullOrEmpty(team) && team != results.Winner)
        {
            results.RunnerUp = team;
        }
    }

    private static void AddSemiFinalists(TournamentNode node, TournamentResults results)
    {
        foreach (var grandChildNode in node.Children ?? Enumerable.Empty<TournamentNode>())
        {
            var team = grandChildNode.Team?.ToLowerAlphaNum();
            if (!string.IsNullOrEmpty(team) &&
                team != results.Winner &&
                team != results.RunnerUp &&
                !results.SemiFinalists.Contains(team))
            {
                results.SemiFinalists.Add(team);
            }
        }
    }
}