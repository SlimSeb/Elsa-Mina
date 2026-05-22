using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Misc.Crypto;

[NamedCommand("bitcoin", Aliases = ["btc"])]
public class BitcoinCommand : CryptoPriceCommand
{
    public BitcoinCommand(IHttpService httpService) : base(httpService) { }

    protected override string CoinId => "bitcoin";
    protected override string DisplayName => "bitcoin";
    protected override string PriceFormat => "F0";
    protected override string ErrorMessageKey => "bitcoin_error";
    public override string HelpMessageKey => "bitcoin_help";
}
