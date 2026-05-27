using ElsaMina.Commands.Replays;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Replays;

public class ReplaysHandlerTest
{
    private ReplaysHandler _replaysHandler;
    private IHttpService _httpService;
    private ITemplatesManager _templatesManager;
    private IRoomsManager _roomsManager;
    private IContext _context;
    private IRoom _room;

    [SetUp]
    public void SetUp()
    {
        var contextFactory = Substitute.For<IContextFactory>();
        _httpService = Substitute.For<IHttpService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _room.TimeZone.Returns(TimeZoneInfo.Utc);
        _context.Room.Returns(_room);
        _replaysHandler =
            new ReplaysHandler(contextFactory, _httpService, _templatesManager);
    }

    [Test]
    public async Task Test_HandleMessage_ShouldNotProcess_WhenReplayPreviewDisabled()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowReplaysPreview, Arg.Any<CancellationToken>())
            .Returns("false");

        // Act
        await _replaysHandler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendAsync<ReplayDto>(default);
    }

    [Test]
    public async Task Test_HandleMessage_ShouldNotProcess_WhenNoReplayLinkFound()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowReplaysPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns("This is a message without a replay link.");

        // Act
        await _replaysHandler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendAsync<ReplayDto>(default);
    }

    [Test]
    public async Task Test_HandleMessage_ShouldSendHtml_WhenReplayInfoFetchedSuccessfully()
    {
        // Arrange
        var replayUrl = "https://replay.pokemonshowdown.com/gen8ou-123456789";
        _room.GetParameterValueAsync(Parameter.ShowReplaysPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns(replayUrl);
        var replayData = new ReplayDto
        {
            Format = "gen8ou",
            Rating = 1400,
            Players = ["player1", "player2"],
            UploadTime = 1625140800,
            Views = 200,
            Log = "sample-log-data"
        };

        _httpService.SendAsync<ReplayDto>(Arg.Is<HttpRequest>(request => request.Uri == replayUrl + ".json"))
            .Returns(new HttpResponse<ReplayDto> { Data = replayData });
        _templatesManager.GetTemplateAsync("Replays/ReplayPreview", Arg.Any<ReplayPreviewViewModel>())
            .Returns("sample-html-template");

        // Act
        await _replaysHandler.HandleMessageAsync(_context);

        // Assert
        await _httpService.Received(1).SendAsync<ReplayDto>(
            Arg.Is<HttpRequest>(request => request.Uri == replayUrl + ".json"));
        _context.Received(1).ReplyHtml("sample-html-template");
    }

    [Test]
    public async Task Test_HandleMessage_ShouldLogError_WhenExceptionOccurs()
    {
        // Arrange
        var replayUrl = "https://replay.pokemonshowdown.com/gen8ou-123456789";
        _room.GetParameterValueAsync(Parameter.ShowReplaysPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns(replayUrl);

        _httpService.SendAsync<ReplayDto>(Arg.Is<HttpRequest>(request => request.Uri == replayUrl + ".json")).Throws(new Exception("Request failed"));

        // Act
        await _replaysHandler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleMessage_ShouldConvertUploadDateToRoomTimeZone_WhenBuildingTemplate()
    {
        // Arrange
        var replayUrl = "https://replay.pokemonshowdown.com/gen8ou-123456789";
        var customTimeZone = TimeZoneInfo.CreateCustomTimeZone("utc-minus-3", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");
        _room.TimeZone.Returns(customTimeZone);
        _room.GetParameterValueAsync(Parameter.ShowReplaysPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns(replayUrl);

        var replayData = new ReplayDto
        {
            Format = "gen8ou",
            Rating = 1400,
            Players = ["player1", "player2"],
            UploadTime = 1625140800,
            Views = 200,
            Log = "sample-log-data"
        };
        var expectedDate = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(replayData.UploadTime), customTimeZone);

        _httpService.SendAsync<ReplayDto>(Arg.Is<HttpRequest>(request => request.Uri == replayUrl + ".json"))
            .Returns(new HttpResponse<ReplayDto> { Data = replayData });
        _templatesManager.GetTemplateAsync("Replays/ReplayPreview", Arg.Any<ReplayPreviewViewModel>())
            .Returns("sample-html-template");

        // Act
        await _replaysHandler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Replays/ReplayPreview",
            Arg.Is<ReplayPreviewViewModel>(viewModel => viewModel.Date == expectedDate));
    }
}
