using System.Globalization;
using System.Text.RegularExpressions;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Lusamine.SlimExpressionParser;

namespace ElsaMina.Commands.CustomCommands;

public partial class AddedCommandsManager : IAddedCommandsManager, IDynamicCommandProvider
{
    private const int MAX_HEIGHT = 300;
    private const int MAX_WIDTH = 400;

    private static readonly Regex EXPRESSION_IDENTIFIER = ExpressionIdentifierRegex();

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IImageService _imageService;
    private readonly IRandomService _randomService;

    public AddedCommandsManager(IBotDbContextFactory dbContextFactory,
        IImageService imageService,
        IRandomService randomService)
    {
        _dbContextFactory = dbContextFactory;
        _imageService = imageService;
        _randomService = randomService;
    }

    public Task<bool> TryExecuteAsync(string commandName, IContext context,
        CancellationToken cancellationToken = default)
    {
        // Les commandes custom sont liées à une room, donc pas utilisables en MP
        if (context.IsPrivateMessage)
        {
            return Task.FromResult(false);
        }

        Log.Information("Trying command {0} as a custom command", commandName);
        return TryExecuteAddedCommand(commandName, context, cancellationToken);
    }

    public async Task<bool> TryExecuteAddedCommand(string commandName, IContext context,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var command = await dbContext.AddedCommands.FindAsync([commandName, context.RoomId], cancellationToken);
        if (command == null)
        {
            return false;
        }

        await ExecuteAddedCommand(command, context, cancellationToken);
        return true;
    }

    public async Task ExecuteAddedCommand(AddedCommand command, IContext context, CancellationToken cancellationToken)
    {
        var content = command.Content;
        if (content.IsValidImageLink())
        {
            await DisplayRemoteImage(context, content, cancellationToken);
            return;
        }

        content = EvaluateContent(content, context);
        context.Reply(content, rankAware: true);
    }

    private async Task DisplayRemoteImage(IContext context, string content, CancellationToken cancellationToken)
    {
        var (width, height) = await _imageService.GetRemoteImageDimensions(content, cancellationToken);
        (width, height) = ImageUtils.ResizeWithSameAspectRatio(width, height, MAX_WIDTH, MAX_HEIGHT);
        context.ReplyHtml($"""<img src="{content}" width="{width}" height="{height}" />""", rankAware: true);
    }

    #region Content Expression Parsing

    internal string EvaluateContent(string content, IContext context)
    {
        var identifiers = BuildIdentifiers(context);
        var functions = BuildFunctions();

        var matches = EXPRESSION_IDENTIFIER.Matches(content);
        foreach (Match match in matches)
        {
            var rawExpression = match.Groups[1].Value;
            var result = EvaluateExpression(rawExpression, identifiers, functions);
            content = content.Replace(match.Value, Convert.ToString(result, CultureInfo.InvariantCulture));
        }

        return content;
    }

    private static object EvaluateExpression(
        string expression,
        Dictionary<string, Func<object>> identifiers,
        Dictionary<string, Func<Arguments, object>> functions)
    {
        var expressionContext = new ExpressionContext();
        foreach (var identifier in identifiers)
        {
            expressionContext.Identifier(identifier.Key, identifier.Value);
        }

        foreach (var function in functions)
        {
            expressionContext.Function(function.Key, function.Value);
        }

        return Evaluator.Evaluate(expression, expressionContext);
    }

    private Dictionary<string, Func<Arguments, object>> BuildFunctions() =>
        new()
        {
            ["choice"] = args => _randomService.RandomElement(args),
            ["dice"] = args => _randomService.NextInt((int)args.Number(0), (int)args.Number(1) + 1),
            ["repeat"] = args => string.Concat(Enumerable.Repeat(args.Text(0), (int)args.Number(1))),
            ["optional"] = args => _randomService.NextDouble() < args.Number(1) ? args[0] : string.Empty,
            ["cos"] = args => Math.Cos(args.Number(0)),
            ["sin"] = args => Math.Sin(args.Number(0)),
            ["tan"] = args => Math.Tan(args.Number(0))
        };

    private Dictionary<string, Func<object>> BuildIdentifiers(IContext context) =>
        new()
        {
            ["command"] = () => context.Command,
            ["author"] = () => context.Sender.Name,
            ["room"] = () => context.Room.Name,
            ["randomMember"] = () => _randomService.RandomElement(context.Room.Users.Select(userKvp => userKvp.Value.Name)),
            ["args"] = () => context.Target,
            ["e"] = () => Math.E,
            ["pi"] = () => Math.PI
        };

    #endregion

    [GeneratedRegex("{([^}]+)}", RegexOptions.None, Constants.REGEX_MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex ExpressionIdentifierRegex();
}
