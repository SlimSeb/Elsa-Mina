using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Battles.Dtos;
using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Commands.Teams;

namespace ElsaMina.Battles;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
)]
[JsonSerializable(typeof(BattleStateDto))]
[JsonSerializable(typeof(LlmDecisionDto))]
[JsonSerializable(typeof(List<bool>))]
[JsonSerializable(typeof(List<PokemonSet>))]
public partial class ElsaMinaBattlesJsonContext : JsonSerializerContext
{
}
