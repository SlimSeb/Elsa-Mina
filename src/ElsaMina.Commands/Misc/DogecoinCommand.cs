using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc;

[NamedCommand("dogecoin", Aliases = ["doge"])]
public class DogecoinCommand : Command
{
    private const string COINGECKO_API_URL =
        "https://api.coingecko.com/api/v3/simple/price?ids=dogecoin&vs_currencies=usd,eur";

    private readonly IHttpService _httpService;

    public DogecoinCommand(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "dogecoin_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result =
                await _httpService.GetAsync<IDictionary<string, IDictionary<string, double>>>(COINGECKO_API_URL,
                    cancellationToken: cancellationToken);
            var coinValues = result.Data["dogecoin"];
            var eur = coinValues["eur"];
            var usd = coinValues["usd"];
            context.Reply($"1 Dogecoin = {eur:F4}€ = {usd:F4}$", rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not fetch dogecoin data from coingecko.");
            context.ReplyLocalizedMessage("dogecoin_error");
        }
    }
}
