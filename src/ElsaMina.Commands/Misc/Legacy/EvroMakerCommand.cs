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
            .Select(word => word.Trim())
            .Where(word => !string.IsNullOrEmpty(word))
            .ToArray();

        if (words.Length < 2)
        {
            return Task.CompletedTask;
        }

        var altCount = 0;
        var builder = new StringBuilder();

        for (var i = 0; i < words.Length; i++)
        {
            var hasAppendedAltString = AppendWord(builder, words[i], i, words.Length, altCount);
            altCount = hasAppendedAltString ? 0 : altCount + 1;
        }

        var newPhrase = builder.ToString().Trim();
        context.Reply(newPhrase, rankAware: true);

        return Task.CompletedTask;
    }

    private bool AppendWord(StringBuilder builder, string word, int wordIndex, int wordCount, int altCount)
    {
        if (wordIndex == 0)
        {
            builder.Append(PickRandom(START_STRINGS)).Append(' ').Append(word).Append(' ');
            return false;
        }

        if (_randomService.NextDouble() > 0.75)
        {
            AppendDecoratedWord(builder, word);
            return false;
        }

        var isLastWord = wordIndex == wordCount - 1;
        if (altCount > 3 && !isLastWord && _randomService.NextDouble() > 0.65)
        {
            builder.Append(word).Append(' ').Append(PickRandom(ALT_STRINGS)).Append(" , ");
            return true;
        }

        if (isLastWord)
        {
            builder.Append(' ').Append(word).Append(' ').Append(PickRandom(COMPLEMENT_STRINGS)).Append(' ');
            return false;
        }

        if (_randomService.NextDouble() > 0.7)
        {
            builder.Append(word).Append(' ').Append(PickRandom(ENDING_STRINGS)).Append(' ');
            return false;
        }

        builder.Append(word).Append(' ');
        return false;
    }

    private void AppendDecoratedWord(StringBuilder builder, string word)
    {
        if (_randomService.NextDouble() > 0.5)
        {
            builder.Append('"').Append(word).Append("\" ");
        }
        else
        {
            builder.Append(':').Append(word).Append(": ");
        }

        if (_randomService.NextDouble() > 0.8)
        {
            builder.Append("kek ");
        }
    }

    private string PickRandom(string[] options)
    {
        return options[_randomService.NextInt(options.Length)];
    }
}