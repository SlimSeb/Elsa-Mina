using ElsaMina.Battles;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles;

[TestFixture]
public class UpdateSearchHandlerTest
{
    private ILadderingService _ladderingService;
    private UpdateSearchHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _ladderingService = Substitute.For<ILadderingService>();
        _handler = new UpdateSearchHandler(_ladderingService);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldReportSearching_WhenServerListsSearches()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "updatesearch", """{"searching":["gen9randombattle"],"games":null}"""], "lobby");

        // Assert
        _ladderingService.Received(1).OnSearchStateUpdated(true, false);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldReportInGame_WhenServerListsGames()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "updatesearch",
                """{"searching":[],"games":{"battle-gen9randombattle-1":"[Gen 9] Random Battle*"}}"""], "lobby");

        // Assert
        _ladderingService.Received(1).OnSearchStateUpdated(false, true);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldReportIdle_WhenNeitherSearchingNorInGame()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "updatesearch", """{"searching":[],"games":null}"""],
            "lobby");

        // Assert
        _ladderingService.Received(1).OnSearchStateUpdated(false, false);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldReadWholeJson_WhenGameTitleContainsPipe()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(
            ["", "updatesearch", """{"searching":[],"games":{"battle-gen9ou-1":"a""", """b"}}"""], "lobby");

        // Assert
        _ladderingService.Received(1).OnSearchStateUpdated(false, true);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnoreMessage_WhenJsonIsInvalid()
    {
        // Act
        await _handler.HandleReceivedMessageAsync(["", "updatesearch", "{not json"], "lobby");

        // Assert
        _ladderingService.DidNotReceiveWithAnyArgs().OnSearchStateUpdated(default, default);
    }
}
