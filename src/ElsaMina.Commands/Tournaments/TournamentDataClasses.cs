using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Tournaments;

public class TournamentUpdateData
{
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("users")]
    public string[] Users { get; set; }
}

public class TournamentUpdate
{
    [JsonPropertyName("isStarted")]
    public bool IsStarted { get; set; }
    
    [JsonPropertyName("bracketData")]
    public TournamentUpdateData BracketData { get; set; }
}

public class TournamentNode
{
    [JsonPropertyName("team")]
    public string Team { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; }
    
    [JsonPropertyName("room")]
    public string Room { get; set; }

    [JsonPropertyName("children")]
    public List<TournamentNode> Children { get; set; } = [];
}

public class RoundRobinTableHeaders
{
    [JsonPropertyName("cols")]
    public List<string> Cols { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<string> Rows { get; set; } = [];
}

public class BracketData
{
    [JsonPropertyName("rootNode")]
    public TournamentNode RootNode { get; set; }

    [JsonPropertyName("tableHeaders")]
    public RoundRobinTableHeaders TableHeaders { get; set; }

    [JsonPropertyName("scores")]
    public List<int> Scores { get; set; } = [];
}

public class TournamentData
{
    [JsonPropertyName("generator")]
    public string Generator { get; set; }
    
    [JsonPropertyName("format")]
    public string Format { get; set; }

    [JsonPropertyName("bracketData")]
    public BracketData BracketData { get; set; }

    [JsonPropertyName("results")]
    public List<List<string>> Results { get; set; }
}