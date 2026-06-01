using System.Globalization;
using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class StartTarotCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private StartTarotCommand _command;
    private IContext _context;
    private IRoom _room;
    private TarotGame _game;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        var sender = MakeUser("starter");
        _context.Room.Returns(_room);
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.Sender.Returns(sender);
        _room.Game.ReturnsNull();

        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("ElsaMina");
        configuration.Trigger.Returns("-");
        var templates = Substitute.For<ITemplatesManager>();
        templates.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(Task.FromResult(string.Empty));

        _game = new TarotGame(Substitute.For<IRandomService>(), templates, configuration,
            Substitute.For<ITarotStatsService>());
        _game.Context = _context;
        _dependencyContainerService.Resolve<TarotGame>().Returns(_game);

        _command = new StartTarotCommand(_dependencyContainerService);
    }

    private static IUser MakeUser(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return user;
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenTarotGameIsActive()
    {
        _room.Game.Returns(Substitute.For<ITarotGame>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tarot_already_running");
        _dependencyContainerService.DidNotReceive().Resolve<TarotGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyOtherGameRunning_WhenDifferentGameIsActive()
    {
        _room.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tarot_other_game_running");
        _dependencyContainerService.DidNotReceive().Resolve<TarotGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateGame_WhenNoGameIsActive()
    {
        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("tarot_game_created", Arg.Any<object>());
            _room.Received(1).Game = _game;
        }
    }
}
