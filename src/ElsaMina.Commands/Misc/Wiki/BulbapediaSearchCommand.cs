using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Wiki;

[NamedCommand("bulbapedia", Aliases = ["bulba"])]
public class BulbapediaSearchCommand : WikiMediaSearchCommand
{
    protected override string ApiUrl => "https://bulbapedia.bulbagarden.net/w/api.php";
    protected override string GetPageUrl(string title) =>
        $"https://bulbapedia.bulbagarden.net/wiki/{Uri.EscapeDataString(title)}";

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "bulbapedia_help";

    public BulbapediaSearchCommand(IHttpService httpService)
        : base(httpService) { }
}
