using Newtonsoft.Json;

namespace ElsaMina.Commands.Tournaments;

public class TournamentUpdateData
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("users")]
    public string[] Users { get; set; }
}

public class TournamentUpdate
{
    [JsonProperty("isStarted", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool IsStarted { get; set; }
    
    [JsonProperty("bracketData")]
    public TournamentUpdateData BracketData { get; set; }
}

public class TournamentNode
{
    [JsonProperty("team")]
    public string Team { get; set; }

    [JsonProperty("state")]
    public string State { get; set; }
    
    [JsonProperty("room")]
    public string Room { get; set; }

    [JsonProperty("children")]
    public List<TournamentNode> Children { get; set; } = [];
}

public class RoundRobinTableHeaders
{
    [JsonProperty("cols")]
    public List<string> Cols { get; set; } = [];

    [JsonProperty("rows")]
    public List<string> Rows { get; set; } = [];
}

public class BracketData
{
    [JsonProperty("rootNode")]
    public TournamentNode RootNode { get; set; }

    [JsonProperty("tableHeaders")]
    public RoundRobinTableHeaders TableHeaders { get; set; }

    [JsonProperty("scores")]
    public List<int> Scores { get; set; } = [];
}

public class TournamentData
{
    [JsonProperty("generator")]
    public string Generator { get; set; }
    
    [JsonProperty("format")]
    public string Format { get; set; }

    [JsonProperty("bracketData")]
    public BracketData BracketData { get; set; }

    [JsonProperty("results")]
    public List<List<string>> Results { get; set; }
}