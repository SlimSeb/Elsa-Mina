using ElsaMina.Commands.Games.PokeRace;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.PokeRace;

[TestFixture]
public class JoinPokeRaceCommandTest
{
    private IContext _context;
    private IRoom _room;
    private IPokeRaceGame _pokeRaceGame;
    private JoinPokeRaceCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _pokeRaceGame = Substitute.For<IPokeRaceGame>();
        _context.Room.Returns(_room);
        _command = new JoinPokeRaceCommand();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenNoPokeRaceGameIsActive()
    {
        _room.Game.Returns((IGame)null);
        _context.Target.Returns("Rapidash");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pokerace_not_running");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotRunning_WhenDifferentGameIsActive()
    {
        var otherGame = Substitute.For<IGame>();
        _room.Game.Returns(otherGame);
        _context.Target.Returns("Rapidash");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pokerace_not_running");
    }

    [Test]
    public async Task Test_RunAsync_ShouldListAvailablePokemon_WhenTargetIsEmpty()
    {
        _room.Game.Returns(_pokeRaceGame);
        _context.Target.Returns(string.Empty);
        _pokeRaceGame.Players.Returns(new Dictionary<string, (string Name, string Pokemon)>());

        await _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Is<string>(msg => msg.Contains("Rapidash")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPokemon_WhenPokemonNotInList()
    {
        _room.Game.Returns(_pokeRaceGame);
        _context.Target.Returns("Pikachu");
        _pokeRaceGame.Players.Returns(new Dictionary<string, (string Name, string Pokemon)>());

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("pokerace_join_invalid_pokemon", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallJoinRaceAsync_WhenValidPokemonProvided()
    {
        _room.Game.Returns(_pokeRaceGame);
        _context.Target.Returns("Rapidash");
        var sender = Substitute.For<IUser>();
        sender.Name.Returns("TestPlayer");
        _context.Sender.Returns(sender);
        _pokeRaceGame.Players.Returns(new Dictionary<string, (string Name, string Pokemon)>());
        _pokeRaceGame.JoinRaceAsync("TestPlayer", "Rapidash")
            .Returns(Task.FromResult((true, "pokerace_join_success", new object[] { "TestPlayer", "Rapidash" })));

        await _command.RunAsync(_context);

        await _pokeRaceGame.Received(1).JoinRaceAsync("TestPlayer", "Rapidash");
        _context.Received(1).ReplyLocalizedMessage("pokerace_join_success", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldBeCaseInsensitiveForPokemonName()
    {
        _room.Game.Returns(_pokeRaceGame);
        _context.Target.Returns("rapidash");
        var sender = Substitute.For<IUser>();
        sender.Name.Returns("TestPlayer");
        _context.Sender.Returns(sender);
        _pokeRaceGame.Players.Returns(new Dictionary<string, (string Name, string Pokemon)>());
        _pokeRaceGame.JoinRaceAsync("TestPlayer", "Rapidash")
            .Returns(Task.FromResult((true, "pokerace_join_success", new object[] { "TestPlayer", "Rapidash" })));

        await _command.RunAsync(_context);

        await _pokeRaceGame.Received(1).JoinRaceAsync("TestPlayer", "Rapidash");
    }
}
