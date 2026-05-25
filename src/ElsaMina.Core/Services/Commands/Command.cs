using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Commands;

public abstract class Command : ICommand
{
    protected Command()
    {
        InitializeNameAndAliasesFromAttribute();
    }

    public string Name { get; private set; }
    public IEnumerable<string> Aliases { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public virtual bool IsAllowedInPrivateMessage => false;
    public virtual bool IsWhitelistOnly => false;
    public virtual bool IsPrivateMessageOnly => false;
    public virtual Rank RequiredRank => Rank.Admin; // todo remplacer ça par une méthode virtual "CanRun" avec contexte
    public virtual string HelpMessageKey => string.Empty;
    public virtual bool IsHidden => false;
    public virtual IEnumerable<string> RoomRestriction => [];
    public virtual string FeatureSwitch => string.Empty;

    protected void ReplyLocalizedHelpMessage(IContext context, bool rankAware = false)
    {
        context.Reply(context.GetString(HelpMessageKey), rankAware: rankAware);
    }

    public abstract Task RunAsync(IContext context, CancellationToken cancellationToken = default);

    private void InitializeNameAndAliasesFromAttribute()
    {
        var type = GetType();
        var commandAttribute = type.GetCommandAttribute();
        Name = commandAttribute?.Name ?? string.Empty;
        Aliases = commandAttribute?.Aliases ?? [];
        Category = DeriveCategoryFromNamespace(type.Namespace);
    }

    private static string DeriveCategoryFromNamespace(string namespaceName)
    {
        // Beaucoup de hardcoding => dégueu
        if (namespaceName == null)
        {
            return string.Empty;
        }

        if (namespaceName.StartsWith("ElsaMina.Battles"))
        {
            return "Battles";
        }

        const string commandProjectPrefix = "ElsaMina.Commands.";
        if (!namespaceName.StartsWith(commandProjectPrefix))
        {
            return string.Empty;
        }

        var remainder = namespaceName[commandProjectPrefix.Length..];
        var dotIndex = remainder.IndexOf('.');
        return dotIndex >= 0 ? remainder[..dotIndex] : remainder;
    }
}