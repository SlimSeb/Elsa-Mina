using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc;

[NamedCommand("ethereum", Aliases = ["eth"])]
public class EthereumCommand : Command
{
    private const string COINGECKO_API_URL =
        "https://api.coingecko.com/api/v3/simple/price?ids=ethereum&vs_currencies=usd,eur";

    private readonly IHttpService _httpService;

    public EthereumCommand(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "ethereum_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result =
                await _httpService.GetAsync<IDictionary<string, IDictionary<string, double>>>(COINGECKO_API_URL,
                    cancellationToken: cancellationToken);
            var coinValues = result.Data["ethereum"];
            var eur = coinValues["eur"];
            var usd = coinValues["usd"];
            context.Reply($"1 Ethereum = {eur:F2}€ = {usd:F2}$", rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not fetch ethereum data from coingecko.");
            context.ReplyLocalizedMessage("ethereum_error");
        }
    }
}
