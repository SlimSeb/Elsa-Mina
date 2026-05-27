using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Dictionary;

[NamedCommand("dicoenglish", Aliases = ["dict"])]
public class DictionaryCommand : Command
{
    private const string DICTIONARY_API_URL =
        "https://dictionaryapi.com/api/v3/references/sd4/json/{0}";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public DictionaryCommand(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "dicoenglish_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var apiKey = _configuration.DictionaryApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Dictionary API key is not configured.");
            return;
        }

        var word = context.Target.Trim();
        var url = string.Format(DICTIONARY_API_URL, Uri.EscapeDataString(word));
        try
        {
            var response = await _httpService.SendAsync<DictionaryApiResponse>(
                HttpRequest.Get(url).WithQueryParameter("key", apiKey), cancellationToken);
            var data = response.Data;

            if (data.IsEmpty)
            {
                context.ReplyLocalizedMessage("dicoenglish_not_found", word);
                return;
            }

            if (data.HasSuggestions)
            {
                context.ReplyLocalizedMessage("dicoenglish_suggestions", word,
                    string.Join(", ", data.Suggestions));
                return;
            }

            var definition = data.Entries[0].ShortDefinitions?.FirstOrDefault();
            if (string.IsNullOrEmpty(definition))
            {
                context.ReplyLocalizedMessage("dicoenglish_not_found", word);
                return;
            }

            context.ReplyLocalizedMessage("dicoenglish_definition", word, definition);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Dictionary command failed");
            context.ReplyLocalizedMessage("dicoenglish_error");
        }
    }
}
