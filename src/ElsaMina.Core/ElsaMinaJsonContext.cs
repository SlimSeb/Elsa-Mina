using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Core.Services.BattleTracker;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.LanguageModel.Google;
using ElsaMina.Core.Services.LanguageModel.Mistral;
using ElsaMina.Core.Services.LanguageModel.OpenAi;
using ElsaMina.Core.Services.Login;
using ElsaMina.Core.Services.RoomInfo;
using ElsaMina.Core.Services.Smogon;
using ElsaMina.Core.Services.UserData;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Logging;

namespace ElsaMina.Core;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(IConfiguration))]
[JsonSerializable(typeof(LogLevel))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, IEnumerable<string>>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(IEnumerable<string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(RoomListQueryResponseDto))]
[JsonSerializable(typeof(ActiveBattleDto))]
[JsonSerializable(typeof(IDictionary<string, ActiveBattleDto>))]
[JsonSerializable(typeof(Dictionary<string, ActiveBattleDto>))]
[JsonSerializable(typeof(UserDetailsDto))]
[JsonSerializable(typeof(UserDetailsRoomDto))]
[JsonSerializable(typeof(IDictionary<string, UserDetailsRoomDto>))]
[JsonSerializable(typeof(Dictionary<string, UserDetailsRoomDto>))]
[JsonSerializable(typeof(RoomInfoDto))]
[JsonSerializable(typeof(IDictionary<string, IReadOnlyList<string>>))]
[JsonSerializable(typeof(Dictionary<string, IReadOnlyList<string>>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(Pokemon[]))]
[JsonSerializable(typeof(Pokemon))]
[JsonSerializable(typeof(Name))]
[JsonSerializable(typeof(Sprite))]
[JsonSerializable(typeof(PokemonType))]
[JsonSerializable(typeof(Talent))]
[JsonSerializable(typeof(Stats))]
[JsonSerializable(typeof(Resistance))]
[JsonSerializable(typeof(Evolution))]
[JsonSerializable(typeof(EvolutionNext))]
[JsonSerializable(typeof(Gender))]
[JsonSerializable(typeof(Dictionary<string, MoveData>))]
[JsonSerializable(typeof(MoveData))]
[JsonSerializable(typeof(SecondaryEffect))]
[JsonSerializable(typeof(StatBoosts))]
[JsonSerializable(typeof(ZMoveInfo))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(PokedexEntry))]
[JsonSerializable(typeof(BaseStats))]
[JsonSerializable(typeof(LoginResponseDto))]
[JsonSerializable(typeof(CurrentUserDto))]
[JsonSerializable(typeof(SmogonUsageDataDto))]
[JsonSerializable(typeof(SmogonUsageInfoDto))]
[JsonSerializable(typeof(SmogonPokemonUsageDataDto))]
[JsonSerializable(typeof(UserDataDto))]
[JsonSerializable(typeof(UserDataRankingDto))]
[JsonSerializable(typeof(IDictionary<string, UserDataRankingDto>))]
[JsonSerializable(typeof(Dictionary<string, UserDataRankingDto>))]
[JsonSerializable(typeof(GptRequestDto))]
[JsonSerializable(typeof(GptConversationItemDto))]
[JsonSerializable(typeof(List<GptConversationItemDto>))]
[JsonSerializable(typeof(GptResponseDto))]
[JsonSerializable(typeof(MistralRequestDto))]
[JsonSerializable(typeof(MistralRequestMessageDto))]
[JsonSerializable(typeof(List<MistralRequestMessageDto>))]
[JsonSerializable(typeof(MistralResponseDto))]
[JsonSerializable(typeof(MistralChoiceDto))]
[JsonSerializable(typeof(List<MistralChoiceDto>))]
[JsonSerializable(typeof(MistralResponseMessageDto))]
[JsonSerializable(typeof(GeminiRequestDto))]
[JsonSerializable(typeof(SystemInstruction))]
[JsonSerializable(typeof(InstructionPart))]
[JsonSerializable(typeof(Content))]
[JsonSerializable(typeof(List<Content>))]
[JsonSerializable(typeof(ContentPart))]
[JsonSerializable(typeof(GeminiResponseDto))]
[JsonSerializable(typeof(Candidate))]
[JsonSerializable(typeof(List<Candidate>))]
[JsonSerializable(typeof(CandidateContent))]
[JsonSerializable(typeof(CandidatePart))]
[JsonSerializable(typeof(UsageMetadata))]
[JsonSerializable(typeof(PromptTokensDetail))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
public partial class ElsaMinaJsonContext : JsonSerializerContext
{
}
