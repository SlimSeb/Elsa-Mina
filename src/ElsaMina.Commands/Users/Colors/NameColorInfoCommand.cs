using System.Drawing;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Users.Colors;

[NamedCommand("namecolor", Aliases = ["name-color", "colorinfo", "namecolorinfo"])]
public class NameColorInfoCommand : Command
{
    private readonly ITemplatesManager _templatesManager;
    private readonly ICustomColorsManager _customColorsManager;

    public NameColorInfoCommand(ITemplatesManager templatesManager, ICustomColorsManager customColorsManager)
    {
        _templatesManager = templatesManager;
        _customColorsManager = customColorsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "name_color_help";
    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Target))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var userId = context.Target.ToLowerAlphaNum();
        Color color;
        Color? originalColor = null;
        if (_customColorsManager.CustomColorsMapping.TryGetValue(userId, out var customColorName))
        {
            color = customColorName.ToColor();
            originalColor = context.Target.ToColor();
        }
        else
        {
            color = context.Target.ToColor();
        }

        var template = await _templatesManager.GetTemplateAsync("Users/Colors/NameColorInfo", new NameColorInfoViewModel
        {
            Culture = context.Culture,
            Name = context.Target,
            Color = color,
            OriginalColor = originalColor
        });
        context.ReplyHtml(template.RemoveNewlines(), rankAware: true);
    }
}