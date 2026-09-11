using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Commands.Ai.Calc;
using ElsaMina.Commands.Ai.TextToSpeech;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;
using ElsaMina.Commands.Games.GuessingGame.Trivia;
using ElsaMina.Commands.Misc.BugReport;
using ElsaMina.Commands.Misc.Dailymotion;
using ElsaMina.Commands.Misc.Dictionary;
using ElsaMina.Commands.Misc.Facts;
using ElsaMina.Commands.Misc.Food;
using ElsaMina.Commands.Misc.Genius;
using ElsaMina.Commands.Misc.LeagueOfLegends;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Commands.Misc.Wiki;
using ElsaMina.Commands.Misc.Youtube;
using ElsaMina.Commands.Replays;
using ElsaMina.Commands.Showdown.Ladder;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Commands.Teams;
using ElsaMina.Commands.Teams.TeamProviders.CoupCritique;
using ElsaMina.Commands.Teams.TeamProviders.Pokepaste;
using ElsaMina.Commands.Teams.TeamProviders.Showdown;
using ElsaMina.Commands.Tournaments;

namespace ElsaMina.Commands;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
)]
// Games Data
[JsonSerializable(typeof(CountriesGameData))]
[JsonSerializable(typeof(List<CapitalCityData>))]
[JsonSerializable(typeof(List<PokemonDescription>))]
[JsonSerializable(typeof(OpenTdbResponse))]

// Teams
[JsonSerializable(typeof(PokemonSet))]
[JsonSerializable(typeof(List<PokemonSet>))]
[JsonSerializable(typeof(IReadOnlyList<PokemonSet>))]
[JsonSerializable(typeof(ShowdownTeamDto))]
[JsonSerializable(typeof(CoupCritiqueResponse))]
[JsonSerializable(typeof(PokepasteTeam))]

// AI & Calc
[JsonSerializable(typeof(CalcRequestDto))]
[JsonSerializable(typeof(ElevenLabsRequestDto))]

// Tournaments
[JsonSerializable(typeof(TournamentData))]
[JsonSerializable(typeof(TournamentUpdate))]

// Webhooks & Issues
[JsonSerializable(typeof(ArcadeEventWebhookBody))]
[JsonSerializable(typeof(GithubIssueRequestDto))]
[JsonSerializable(typeof(GithubIssueResponseDto))]

// Dictionary
[JsonSerializable(typeof(DictionaryApiResponse))]
[JsonSerializable(typeof(List<DictionaryApiEntry>))]

// League of Legends
[JsonSerializable(typeof(MatchDto))]
[JsonSerializable(typeof(RiotAccountDto))]
[JsonSerializable(typeof(List<LeagueEntryDto>))]

// Replays & Showdown
[JsonSerializable(typeof(ReplayDto))]
[JsonSerializable(typeof(LadderDto))]
[JsonSerializable(typeof(RankingDataDto))]
[JsonSerializable(typeof(List<RankingDataDto>))]
[JsonSerializable(typeof(IEnumerable<RankingDataDto>))]

// Media & Misc
[JsonSerializable(typeof(ElsaMina.Commands.Misc.Youtube.Thumbnail), TypeInfoPropertyName = "YouTubeThumbnail")]
[JsonSerializable(typeof(ElsaMina.Commands.Misc.Wiki.Thumbnail), TypeInfoPropertyName = "WikiThumbnail")]
[JsonSerializable(typeof(UnsplashPhotoDto))]
[JsonSerializable(typeof(YouTubeSearchResponse))]
[JsonSerializable(typeof(YouTubeVideoListResponse))]
[JsonSerializable(typeof(VideoListResponse))]
[JsonSerializable(typeof(KlipySearchResponse))]
[JsonSerializable(typeof(FactDto))]
[JsonSerializable(typeof(GeniusSearchResult))]

// Wiki
[JsonSerializable(typeof(WikipediaExtractResponse))]
[JsonSerializable(typeof(WikipediaApiSearchResponse))]
[JsonSerializable(typeof(PokepediaParseResponse))]

// Food
[JsonSerializable(typeof(SpoonacularRandomResponse))]
[JsonSerializable(typeof(SpoonacularSearchResponse))]
[JsonSerializable(typeof(SpoonacularIngredientResponse))]
[JsonSerializable(typeof(List<SpoonacularAnalyzedInstruction>))]

// Primitives & General Collections
[JsonSerializable(typeof(IDictionary<string, IDictionary<string, double>>))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, double>>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
public partial class ElsaMinaCommandsJsonContext : JsonSerializerContext
{
}
