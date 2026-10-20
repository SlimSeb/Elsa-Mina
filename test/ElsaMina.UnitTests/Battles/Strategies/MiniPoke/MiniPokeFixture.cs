using System.Text.Json.Serialization;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

/// <summary>
/// One battle seen from one side, recorded by tools/minipoke/make_fixtures.py.
/// </summary>
public class MiniPokeFixture
{
    [JsonPropertyName("battle_tag")]
    public string BattleTag { get; set; }

    [JsonPropertyName("player_role")]
    public string PlayerRole { get; set; }

    /// <summary>
    /// Every protocol line the battle received, in order.
    /// </summary>
    [JsonPropertyName("lines")]
    public List<string> Lines { get; set; } = [];

    /// <summary>
    /// Lines poke-env raised on (it then dropped the rest of their frame).
    /// </summary>
    [JsonPropertyName("error_lines")]
    public List<int> ErrorLines { get; set; } = [];

    [JsonPropertyName("decisions")]
    public List<MiniPokeFixtureDecision> Decisions { get; set; } = [];
}
