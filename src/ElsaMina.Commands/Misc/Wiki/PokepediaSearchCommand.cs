using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Wiki;

[NamedCommand("pokepedia", Aliases = ["ppedia"])]
public class PokepediaSearchCommand : WikiMediaSearchCommand
{
    protected override string ApiUrl => "https://www.pokepedia.fr/api.php";
    protected override string GetPageUrl(string title) =>
        $"https://www.pokepedia.fr/{Uri.EscapeDataString(title)}";

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "pokepedia_help";

    public PokepediaSearchCommand(IHttpService httpService)
        : base(httpService) { }
}
