using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Misc;

[NamedCommand("ethereum", Aliases = ["eth"])]
public class EthereumCommand : CryptoPriceCommand
{
    public EthereumCommand(IHttpService httpService) : base(httpService) { }

    protected override string CoinId => "ethereum";
    protected override string DisplayName => "Ethereum";
    protected override string PriceFormat => "F2";
    protected override string ErrorMessageKey => "ethereum_error";
    public override string HelpMessageKey => "ethereum_help";
}
