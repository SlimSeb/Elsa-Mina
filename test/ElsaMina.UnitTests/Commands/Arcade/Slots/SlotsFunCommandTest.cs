using System.Globalization;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Arcade.Slots;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Slots;

public class SlotsFunCommandTest
{
    private IRandomService _randomService;
    private IClockService _clockService;
    private ITemplatesManager _templatesManager;
    private IArcadeEventsService _arcadeEventsService;
    private IContext _context;
    private IUser _sender;
    private SlotsFunCommand _command;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        _clockService = Substitute.For<IClockService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();

        _sender.Name.Returns("TestUser");
        _sender.UserId.Returns("testuser");
        _context.Sender.Returns(_sender);
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.IsPrivateMessage.Returns(true);
        _context.GetString(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(callInfo => callInfo.Arg<string>());

        _randomService.NextInt(Arg.Any<int>()).Returns(0);
        _randomService.NextInt(100).Returns(99); // default to win path to avoid infinite lose reroll loop
        _randomService.RandomElement(Arg.Any<IEnumerable<string>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<string>>().First());

        _clockService.CurrentUtcDateTimeOffset.Returns(DateTimeOffset.UtcNow);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("html"));

        _command = new SlotsFunCommand(_randomService, _clockService, _templatesManager, _arcadeEventsService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithPmBlacklistMessage_WhenRoomIsBlacklisted()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("franais");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Is<string>(s =>
            s.StartsWith($"/pm {_sender.UserId}") && s.Contains("slots_blacklisted_room")));
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCheckBlacklist_WhenIsPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.RoomId.Returns("franais");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().Reply(Arg.Any<string>());
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithCooldownMessage_WhenUserHasRecentlyUsedCommand()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _clockService.CurrentUtcDateTimeOffset.Returns(fixedTime);
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("allowedroom");

        // First call sets the cooldown
        await _command.RunAsync(_context);
        _context.ClearReceivedCalls();
        _templatesManager.ClearReceivedCalls();

        // Act - second call within 24h window
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Is<string>(s =>
            s.StartsWith($"/pm {_sender.UserId}") && s.Contains("slots_cooldown")));
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldProceed_WhenCooldownHasExpired()
    {
        // Arrange
        var initialTime = DateTimeOffset.UtcNow;
        var expiredTime = initialTime.AddHours(25);
        _clockService.CurrentUtcDateTimeOffset.Returns(initialTime, expiredTime);
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("allowedroom");

        // First call sets cooldown at initialTime
        await _command.RunAsync(_context);
        _templatesManager.ClearReceivedCalls();

        // Act - second call 25h later, cooldown should be expired
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotEnforceCooldown_WhenIsPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);

        // Act - two consecutive PM calls
        await _command.RunAsync(_context);
        await _command.RunAsync(_context);

        // Assert - template called both times, no cooldown reply
        _context.DidNotReceive().Reply(Arg.Is<string>(s => s.Contains("slots_cooldown")));
        await _templatesManager.Received(2).GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendHtmlWithTripleMatchingImages_WhenWinConditionIsMet()
    {
        // Arrange
        // resultIndex=0 (bulbasaur), winThreshold=68; NextInt(100)=99 >= 68 → win
        _randomService.NextInt(12).Returns(0);
        _randomService.NextInt(100).Returns(99);
        const string bulbasaurImage = "https://www.shinyhunters.com/images/regular/1.gif";

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Slots/SlotsFun",
            Arg.Is<SlotsFunViewModel>(vm =>
                vm.SlotImageOne == bulbasaurImage &&
                vm.SlotImageTwo == bulbasaurImage &&
                vm.SlotImageThree == bulbasaurImage));
        _context.Received(1).ReplyHtml("html");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendHtmlWithDifferentImages_WhenLoseConditionIsMet()
    {
        // Arrange
        // resultIndex=0 (bulbasaur), winThreshold=68; NextInt(100)=0 < 68 → lose
        // lose slot indices: 0 (bulbasaur), 1 (squirtle), 2 (charmander)
        _randomService.NextInt(12).Returns(0, 0, 1, 2);
        _randomService.NextInt(100).Returns(0);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Slots/SlotsFun",
            Arg.Is<SlotsFunViewModel>(vm =>
                vm.SlotImageOne == "https://www.shinyhunters.com/images/regular/1.gif" &&
                vm.SlotImageTwo == "https://www.shinyhunters.com/images/regular/2.gif" &&
                vm.SlotImageThree == "https://www.shinyhunters.com/images/regular/3.gif"));
        _context.Received(1).ReplyHtml("html");
    }

    [Test]
    public async Task Test_RunAsync_ShouldRerollLoseSlots_WhenAllThreeInitialSlotsAreEqual()
    {
        // Arrange
        // First three lose spins: all 0 (equal) → reroll
        // Second three lose spins: 0, 1, 2 (different) → accepted
        _randomService.NextInt(12).Returns(0, 0, 0, 0, 0, 1, 2);
        _randomService.NextInt(100).Returns(0);

        // Act
        await _command.RunAsync(_context);

        // Assert - template called exactly once with the rerolled (different) images
        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Slots/SlotsFun",
            Arg.Is<SlotsFunViewModel>(vm =>
                vm.SlotImageOne == "https://www.shinyhunters.com/images/regular/1.gif" &&
                vm.SlotImageTwo == "https://www.shinyhunters.com/images/regular/2.gif" &&
                vm.SlotImageThree == "https://www.shinyhunters.com/images/regular/3.gif"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassWinOutcomeMessage_WhenWinConditionIsMet()
    {
        // Arrange
        _randomService.NextInt(12).Returns(0);
        _randomService.NextInt(100).Returns(99);
        _context.GetString("slots_description_bulbasaur").Returns("une plante");
        _context.GetString(Arg.Any<string>(), Arg.Any<object[]>()).Returns("Tu as gagné une plante!");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Slots/SlotsFun",
            Arg.Is<SlotsFunViewModel>(vm => vm.OutcomeMessage.Contains("Tu as gagné une plante!")));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassSenderNameAndColor_ToTemplate()
    {
        // Arrange
        _sender.Name.Returns("Sacha");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Arcade/Slots/SlotsFun",
            Arg.Is<SlotsFunViewModel>(vm => vm.UserName == "Sacha"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMutedEvent_WhenGamesAreMutedAndNotPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("arcade");
        _arcadeEventsService.AreGamesMuted("arcade").Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCheckMute_WhenIsPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _arcadeEventsService.AreGamesMuted(Arg.Any<string>()).Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage("games_muted_event");
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }
}
