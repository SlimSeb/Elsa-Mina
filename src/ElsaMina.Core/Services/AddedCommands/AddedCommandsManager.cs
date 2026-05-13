using System.Text.RegularExpressions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using NCalc;

namespace ElsaMina.Core.Services.AddedCommands;

public class AddedCommandsManager : IAddedCommandsManager
{
    private const int MAX_HEIGHT = 300;
    private const int MAX_WIDTH = 400;

    private static readonly Regex EXPRESSION_IDENTIFIER =
        new("{([^}]+)}", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

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

    public async Task<bool> TryExecuteAddedCommand(string commandName, IContext context)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var command = await dbContext.AddedCommands.FindAsync(commandName, context.RoomId);
        if (command == null)
        {
            return false;
        }

        await ExecuteAddedCommand(command, context);
        return true;
    }

    public async Task ExecuteAddedCommand(AddedCommand command, IContext context)
    {
        var content = command.Content;
        if (content.IsValidImageLink())
        {
            await DisplayRemoteImage(context, content);
            return;
        }

        content = EvaluateContent(content, context);
        context.Reply(content, rankAware: true);
    }

    private async Task DisplayRemoteImage(IContext context, string content)
    {
        var (width, height) = await _imageService.GetRemoteImageDimensions(content);
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
            var result = EvaluateExpression(new Expression(rawExpression), identifiers, functions);
            content = content.Replace(match.Value, result.ToString());
        }

        return content;
    }

    private static object EvaluateExpression(
        Expression expression,
        Dictionary<string, Func<object>> identifiers,
        Dictionary<string, Func<object[], object>> functions)
    {
        foreach (var identifier in identifiers)
        {
            expression.Parameters[identifier.Key] = identifier.Value.Invoke();
        }

        foreach (var function in functions)
        {
            expression.EvaluateFunction += (name, args) =>
            {
                if (name == function.Key)
                {
                    args.Result = function.Value.DynamicInvoke((object)args.EvaluateParameters(CancellationToken.None));
                }
            };
        }

        return expression.Evaluate();
    }

    private Dictionary<string, Func<object[], object>> BuildFunctions() =>
        new()
        {
            ["choice"] = args => _randomService.RandomElement(args),
            ["dice"] = args => _randomService.NextInt(Convert.ToInt32(args[0].ToString()), Convert.ToInt32(args[1].ToString()) + 1),
            ["repeat"] = args => string.Concat(Enumerable.Repeat(args[0], Convert.ToInt32(args[1].ToString()))),
            ["optional"] = args => _randomService.NextDouble() < Convert.ToDouble(args[1].ToString()) ? args[0] : string.Empty,
            ["cos"] = args => Math.Cos(Convert.ToDouble(args[0].ToString())),
            ["sin"] = args => Math.Sin(Convert.ToDouble(args[0].ToString())),
            ["tan"] = args => Math.Tan(Convert.ToDouble(args[0].ToString()))
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
}