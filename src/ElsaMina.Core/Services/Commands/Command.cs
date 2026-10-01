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
    public virtual Rank RequiredRank => Rank.Admin;
    public virtual string HelpMessageKey => string.Empty;
    public virtual bool IsHidden => false;
    public virtual IEnumerable<string> RoomRestriction => [];
    public virtual bool RunsInMessageOrder => false;

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
        Category = commandAttribute?.Category ?? DeriveCategoryFromNamespace(type.Namespace);
    }

    private static string DeriveCategoryFromNamespace(string namespaceName)
    {
        const string rootNamespace = "ElsaMina";
        const string commandsProject = "Commands";
        const string coreProject = "Core";

        var segments = namespaceName?.Split('.') ?? [];
        if (segments.Length < 2 || segments[0] != rootNamespace)
        {
            return string.Empty;
        }

        if (segments[1] == commandsProject)
        {
            return segments.Length > 2 ? segments[2] : string.Empty;
        }

        return segments[1] == coreProject ? string.Empty : segments[1];
    }
}