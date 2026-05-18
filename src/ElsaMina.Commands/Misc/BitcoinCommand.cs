using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc;

[NamedCommand("bitcoin", Aliases = ["btc"])]
public class BitcoinCommand : Command
{
    private const string COINDESK_API_URL =
        "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd,eur";

    private readonly IHttpService _httpService;

    public BitcoinCommand(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result =
                await _httpService.GetAsync<IDictionary<string, IDictionary<string, int>>>(COINDESK_API_URL,
                    cancellationToken: cancellationToken);
            var coinValues = result.Data["bitcoin"];
            var eur = coinValues["eur"];
            var usd = coinValues["usd"];
            context.Reply($"1 bitcoin = {eur}€ = {usd}$", rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not fetch data from coindesk.");
            context.ReplyLocalizedMessage("bitcoin_error");
        }
    }
}