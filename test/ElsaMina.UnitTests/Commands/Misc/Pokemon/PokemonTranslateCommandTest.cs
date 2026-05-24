using ElsaMina.Commands.Misc.Pokemon;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Dex;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Pokemon;

[TestFixture]
public class PokemonTranslateCommandTest
{
    private IDexManager _dexManager;
    private IContext _context;
    private PokemonTranslateCommand _command;

    private static readonly ElsaMina.Core.Services.Dex.Pokemon[] TEST_POKEDEX =
    [
        new()
        {
            PokedexId = 1,
            Name = new Name { English = "Bulbasaur", French = "Bulbizarre", Japanese = "フシギダネ" }
        },
        new()
        {
            PokedexId = 25,
            Name = new Name { English = "Pikachu", French = "Pikachu", Japanese = "ピカチュウ" }
        }
    ];

    [SetUp]
    public void SetUp()
    {
        _dexManager = Substitute.For<IDexManager>();
        _dexManager.Pokedex.Returns(TEST_POKEDEX);

        _context = Substitute.For<IContext>();

        _command = new PokemonTranslateCommand(_dexManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithRequiredMessage_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("pokemon_name_required");
        _context.DidNotReceive().Reply(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithTranslations_WhenFoundByEnglishName()
    {
        // Arrange
        _context.Target.Returns("Bulbasaur");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(
            "**#1 Bulbasaur** - EN: Bulbasaur | FR: Bulbizarre | JP: フシギダネ",
            rankAware: true);
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithTranslations_WhenFoundByFrenchName()
    {
        // Arrange
        _context.Target.Returns("Bulbizarre");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(
            "**#1 Bulbasaur** - EN: Bulbasaur | FR: Bulbizarre | JP: フシギダネ",
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithTranslations_WhenFoundByJapaneseName()
    {
        // Arrange
        _context.Target.Returns("ピカチュウ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(
            "**#25 Pikachu** - EN: Pikachu | FR: Pikachu | JP: ピカチュウ",
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldBeCaseInsensitive_WhenSearchingByName()
    {
        // Arrange
        _context.Target.Returns("bULBASAUR");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(
            "**#1 Bulbasaur** - EN: Bulbasaur | FR: Bulbizarre | JP: フシギダネ",
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNotFound_WhenPokemonDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("Missingno");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("pokemon_translate_not_found", "Missingno");
        _context.DidNotReceive().Reply(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldReturnTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }
}
