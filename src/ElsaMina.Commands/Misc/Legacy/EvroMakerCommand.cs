using System.Text;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;

namespace ElsaMina.Commands.Misc.Legacy;

// Note : this is dumb shit kept here for historical purposes 
[NamedCommand("evromaker")]
public class EvroMakerCommand : Command
{
    private static readonly string[] START_STRINGS =
    [
        "Btw", "Euh pk", "ba enft", "enft", "ba pk", "squoi les bails", "kek", "jvé"
    ];

    private static readonly string[] ALT_STRINGS = ["kek", "ué ué", "mdrrr", "(ba après g 13 ans)", "dcp c normal"];

    private static readonly string[] COMPLEMENT_STRINGS =
    [
        "(je rigole ofc)", "j'vous goumasse N_n", "N_n jvou goumasse", "bref jvou goumasse", "bref",
        "staiv", "bref go goulag ??", "(apres g 13 apres)", "dcp c norml", "mdrrrrr", "plz", "PLZ",
        "plZ", "jej starf", "plZzz"
    ];

    private static readonly string[] ENDING_STRINGS =
    [
        "Jsp", ":)", "tbh ué", "plz", "?_?", "N_n", "mdr mé non", "zetes con", "ué ué", "mdrrrr", "ui",
        "cv", "Oo", "X3", "oque", "rllent", "leeel", "when", "keeeeek"
    ];

    private readonly IRandomService _randomService;

    public EvroMakerCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsHidden => true;
    public override bool IsWhitelistOnly => true;
    public override string HelpMessageKey => "evromaker_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            return Task.CompletedTask;
        }

        var words = context.Target.Split(' ')
            .Select(w => w.Trim())
            .Where(w => !string.IsNullOrEmpty(w))
            .ToArray();

        if (words.Length < 2)
        {
            return Task.CompletedTask;
        }

        var count = 0;
        var altCount = 0;
        var sb = new StringBuilder();

        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word)) continue;

            if (count == 0)
            {
                sb.Append(START_STRINGS[_randomService.NextInt(START_STRINGS.Length)])
                    .Append(' ')
                    .Append(word)
                    .Append(' ');
                count++;
                altCount++;
                continue;
            }

            if (_randomService.NextDouble() > 0.75)
            {
                if (_randomService.NextDouble() > 0.5)
                {
                    sb.Append('"').Append(word).Append("\" ");
                    if (_randomService.NextDouble() > 0.8) sb.Append("kek ");
                }
                else
                {
                    sb.Append(':').Append(word).Append(": ");
                    if (_randomService.NextDouble() > 0.8) sb.Append("kek ");
                }

                count++;
                altCount++;
                continue;
            }

            if (altCount > 3 && count != words.Length - 1 && _randomService.NextDouble() > 0.65)
            {
                sb.Append(word).Append(' ')
                    .Append(ALT_STRINGS[_randomService.NextInt(ALT_STRINGS.Length)])
                    .Append(" , ");
                altCount = 0;
                count++;
                continue;
            }

            if (count == words.Length - 1)
            {
                sb.Append(' ')
                    .Append(word)
                    .Append(' ')
                    .Append(COMPLEMENT_STRINGS[_randomService.NextInt(COMPLEMENT_STRINGS.Length)])
                    .Append(' ');
                count++;
                altCount++;
                continue;
            }

            if (_randomService.NextDouble() > 0.7)
            {
                sb.Append(word).Append(' ')
                    .Append(ENDING_STRINGS[_randomService.NextInt(ENDING_STRINGS.Length)])
                    .Append(' ');
                count++;
                altCount++;
                continue;
            }

            sb.Append(word).Append(' ');
            count++;
            altCount++;
        }

        var newPhrase = sb.ToString().Trim();
        context.Reply(newPhrase, rankAware: true);

        return Task.CompletedTask;
    }
}