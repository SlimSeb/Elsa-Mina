using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Crypto;

public abstract class CryptoPriceCommand : Command
{
    private const string COINGECKO_API_URL =
        "https://api.coingecko.com/api/v3/simple/price?ids={0}&vs_currencies=usd,eur";

    private readonly IHttpService _httpService;

    protected CryptoPriceCommand(IHttpService httpService)
    {
        _httpService = httpService;
    }

    protected abstract string CoinId { get; }
    protected abstract string DisplayName { get; }
    protected abstract string PriceFormat { get; }
    protected abstract string ErrorMessageKey { get; }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = string.Format(COINGECKO_API_URL, CoinId);
            var result =
                await _httpService.SendAsync<IDictionary<string, IDictionary<string, double>>>(
                    HttpRequest.Get(url), cancellationToken);
            var coinValues = result.Data[CoinId];
            var eur = coinValues["eur"];
            var usd = coinValues["usd"];
            context.Reply(
                $"1 {DisplayName} = {eur.ToString(PriceFormat, context.Culture)}€ = {usd.ToString(PriceFormat, context.Culture)}$",
                rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not fetch {CoinId} data from coingecko.", CoinId);
            context.ReplyLocalizedMessage(ErrorMessageKey);
        }
    }
}