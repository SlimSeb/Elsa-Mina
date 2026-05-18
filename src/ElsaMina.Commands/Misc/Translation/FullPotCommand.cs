using System.Text;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using Lusamine.GTranslate;

namespace ElsaMina.Commands.Misc.Translation;

[NamedCommand("fullpot")]
public class FullPotCommand : Command
{
    private const int LANGUAGES_COUNT = 16;

    private readonly IRandomService _randomService;

    public FullPotCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override Rank RequiredRank => Rank.RoomOwner;
    public override string HelpMessageKey => "fullpot_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyLocalizedMessage(HelpMessageKey);
            return;
        }

        var pot = _randomService.RandomSample(Languages.All.Keys, LANGUAGES_COUNT).ToList();
        pot.Insert(0, "fr");
        pot.Add("fr");

        var currentText = context.Target.Trim();
        var box = new StringBuilder("<details><summary>FullPot :]</summary>");
        box.Append($"Original message: {currentText}<br />");

        try
        {
            await using var translator = new Translator();
            for (var i = 0; i < pot.Count - 1; i++)
            {
                var result = await translator.TranslateAsync(currentText, pot[i + 1], pot[i], cancellationToken);
                currentText = result.Text;
                box.Append($"{pot[i]}->{pot[i + 1]}: {currentText}<br />");
            }
        }
        catch (Exception)
        {
            context.ReplyLocalizedMessage("googletranslate_error");
            return;
        }

        box.Append("</details>");
        context.ReplyHtml(box.ToString());
    }
}
