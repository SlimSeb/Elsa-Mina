using ElsaMina.Commands.Games.PokeRace;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.PokeRace;

[TestFixture]
public class StartPokeRaceCommandTest
{
    private Func<PokeRaceGame> _gameFactory;
    private IContext _context;
    private IRoom _room;
    private StartPokeRaceCommand _command;

    [SetUp]
    public void SetUp()
    {
        _gameFactory = Substitute.For<Func<PokeRaceGame>>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _context.Room.Returns(_room);
        _command = new StartPokeRaceCommand(_gameFactory);
    }

    private static PokeRaceGame BuildGame()
    {
        var randomService = Substitute.For<IRandomService>();
        var templatesManager = Substitute.For<ITemplatesManager>();
        var systemService = Substitute.For<ISystemService>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        return new PokeRaceGame(randomService, templatesManager, TimeSpan.FromHours(1), TimeSpan.FromHours(1),
            systemService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyRunning_WhenPokeRaceGameIsRunning()
    {
        var existingGame = Substitute.For<IPokeRaceGame>();
        _room.Game.Returns(existingGame);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pokerace_already_running");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyOtherGameRunning_WhenDifferentGameIsRunning()
    {
        var otherGame = Substitute.For<IGame>();
        _room.Game.Returns(otherGame);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pokerace_other_game_running");
        _gameFactory.DidNotReceive()();
    }

    [Test]
    public async Task Test_RunAsync_ShouldStartGame_WhenNoGameIsRunning()
    {
        _room.Game.Returns((IGame)null);
        var game = BuildGame();
        _gameFactory().Returns(game);

        await _command.RunAsync(_context);

        _context.DidNotReceive().ReplyLocalizedMessage("pokerace_already_running");
        _context.DidNotReceive().ReplyLocalizedMessage("pokerace_other_game_running");
        _gameFactory.Received(1)();
    }

    [Test]
    public async Task Test_RunAsync_ShouldAssignContextAndRoomGame_WhenStartingGame()
    {
        _room.Game.Returns((IGame)null);
        var game = BuildGame();
        _gameFactory().Returns(game);

        await _command.RunAsync(_context);

        Assert.That(game.Context, Is.SameAs(_context));
        _room.Received(1).Game = game;
    }
}
