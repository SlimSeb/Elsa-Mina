using ElsaMina.Commands.Tournaments.Hebdo;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.Hebdo;

public class TourHelpCommandTest
{
    private IContext _context;
    private TourHelpCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns("Available tour commands:");
        _command = new TourHelpCommand();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Arrange
        // (no additional setup)

        // Act
        var rank = _command.RequiredRank;

        // Assert
        Assert.That(rank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallReplyHtml()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeTourHelpTitleFromLocalization()
    {
        // Arrange
        _context.GetString("tour_help_title").Returns("Commandes de tournois:");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Is<string>(html => html.StartsWith("Commandes de tournois:")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeSharedpowerInHtml()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Is<string>(html => html.Contains("sharedpower")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeAllCommandNamesInHtml()
    {
        // Arrange
        // (no additional setup)

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Is<string>(html =>
            html.Contains("hebdosv") &&
            html.Contains("hebdoss") &&
            html.Contains("hebdosm") &&
            html.Contains("hebdoaaa") &&
            html.Contains("hebdobh") &&
            html.Contains("hebdomnm") &&
            html.Contains("hebdogg") &&
            html.Contains("hebdostab") &&
            html.Contains("hebdopic") &&
            html.Contains("hebdoinhe") &&
            html.Contains("hebdocamo") &&
            html.Contains("hebdonfe") &&
            html.Contains("hebdo1v1") &&
            html.Contains("hebdoag") &&
            html.Contains("hebdolcuu") &&
            html.Contains("hebdoubersuu") &&
            html.Contains("hebdozu") &&
            html.Contains("hebdoadvru") &&
            html.Contains("hebdobwru") &&
            html.Contains("hebdoorasru") &&
            html.Contains("hebdosmru") &&
            html.Contains("hebdossru")));
    }
}
