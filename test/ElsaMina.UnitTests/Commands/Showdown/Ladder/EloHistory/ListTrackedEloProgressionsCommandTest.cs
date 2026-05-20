using ElsaMina.Commands.Showdown.Ladder.EloHistory;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder.EloHistory;

public class ListTrackedEloProgressionsCommandTest
{
    private IEloProgressionManager _eloProgressionManager;
    private ListTrackedEloProgressionsCommand _command;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _eloProgressionManager = Substitute.For<IEloProgressionManager>();
        _command = new ListTrackedEloProgressionsCommand(_eloProgressionManager);
        _context = Substitute.For<IContext>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNone_WhenNoUsersAreTracked()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("list_elo_progressions_none");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenUsersAreTracked()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([
            new EloTrackedUser("gen9ou", "alice"),
            new EloTrackedUser("gen8ou", "bob")
        ]);
        _context.GetString("list_elo_progressions_entry", Arg.Any<object[]>())
            .Returns(callInfo => $"{callInfo.ArgAt<object[]>(1)[0]} in {callInfo.ArgAt<object[]>(1)[1]}");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage("list_elo_progressions_none");
        _context.Received(1).ReplyHtml(Arg.Is<string>(html =>
            html.Contains("alice") && html.Contains("gen9ou") &&
            html.Contains("bob") && html.Contains("gen8ou")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldOutputEntriesSortedByFormatThenUserId()
    {
        // Arrange
        _eloProgressionManager.GetAllTrackedUsers().Returns([
            new EloTrackedUser("gen9ou", "zelda"),
            new EloTrackedUser("gen8ou", "bob"),
            new EloTrackedUser("gen9ou", "alice"),
        ]);

        var outputOrder = new List<string>();
        _context.GetString("list_elo_progressions_entry", Arg.Any<object[]>())
            .Returns(callInfo =>
            {
                var args = callInfo.ArgAt<object[]>(1);
                var entry = $"{args[0]} in {args[1]}";
                outputOrder.Add(entry);
                return entry;
            });

        // Act
        await _command.RunAsync(_context);

        // Assert
        Assert.That(outputOrder, Is.EqualTo(new[]
        {
            "bob in gen8ou",
            "alice in gen9ou",
            "zelda in gen9ou"
        }));
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }
}
