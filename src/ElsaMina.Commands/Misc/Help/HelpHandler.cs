using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Misc.Help;

public class HelpHandler : PrivateMessageHandler
{
    private readonly HashSet<string> _repliedToUsers = [];

    private readonly IConfiguration _configuration;

    public HelpHandler(IContextFactory contextFactory, IConfiguration configuration) : base(contextFactory)
    {
        _configuration = configuration;
    }

    public override Task HandleMessageAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (_repliedToUsers.Contains(context.Sender.UserId)
            || context.Message.StartsWith(_configuration.Trigger) ||
            context.Sender.UserId == _configuration.Name.ToLowerAlphaNum())
        {
            return Task.CompletedTask;
        }

        context.ReplyLocalizedMessage("help_handler_greeting", _configuration.Name, _configuration.Trigger,
            "https://github.com/SlimSeb/Elsa-Mina");

        _repliedToUsers.Add(context.Sender.UserId);

        return Task.CompletedTask;
    }
}