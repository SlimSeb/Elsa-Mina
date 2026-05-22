using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Commands.Misc.Crypto;

[NamedCommand("dogecoin", Aliases = ["doge"])]
public class DogecoinCommand : CryptoPriceCommand
{
    public DogecoinCommand(IHttpService httpService) : base(httpService) { }

    protected override string CoinId => "dogecoin";
    protected override string DisplayName => "Dogecoin";
    protected override string PriceFormat => "F4";
    protected override string ErrorMessageKey => "dogecoin_error";
    public override string HelpMessageKey => "dogecoin_help";
}
