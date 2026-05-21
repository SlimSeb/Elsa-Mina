using System.Globalization;
using ElsaMina.Commands.Games.RockPaperScissors;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.RockPaperScissors;

[TestFixture]
public class StartRpsCommandTest
{
    private IDependencyContainerService _mockDependencyContainerService;
    private StartRpsCommand _command;
    private IContext _mockContext;
    private IRoom _mockRoom;
    private RpsGame _game;

    [SetUp]
    public void SetUp()
    {
        _mockDependencyContainerService = Substitute.For<IDependencyContainerService>();
        _mockContext = Substitute.For<IContext>();
        _mockRoom = Substitute.For<IRoom>();

        _mockContext.Room.Returns(_mockRoom);
        _mockContext.Culture.Returns(CultureInfo.InvariantCulture);
        _mockRoom.Game.ReturnsNull();

        var mockConfig = Substitute.For<IConfiguration>();
        mockConfig.Name.Returns("ElsaMina");
        mockConfig.Trigger.Returns("-");
        var mockTemplates = Substitute.For<ITemplatesManager>();
        mockTemplates.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _game = new RpsGame(mockConfig, Substitute.For<ISystemService>(), mockTemplates);
        _game.Context = _mockContext;
        _mockDependencyContainerService.Resolve<RpsGame>().Returns(_game);

        _command = new StartRpsCommand(_mockDependencyContainerService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenRpsGameIsActive()
    {
        _mockRoom.Game.Returns(Substitute.For<IRpsGame>());

        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_already_running");
        _mockDependencyContainerService.DidNotReceive().Resolve<RpsGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyOtherGameRunning_WhenDifferentGameIsActive()
    {
        _mockRoom.Game.Returns(Substitute.For<IGame>());

        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_other_game_running");
        _mockDependencyContainerService.DidNotReceive().Resolve<RpsGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCreateGame_WhenNoGameIsActive()
    {
        await _command.RunAsync(_mockContext);

        _mockContext.Received(1).ReplyLocalizedMessage("rps_game_created");
        _mockRoom.Received(1).Game = _game;
    }
}
