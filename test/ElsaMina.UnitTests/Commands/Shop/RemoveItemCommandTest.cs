using ElsaMina.Commands.Shop;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Shop;

[TestFixture]
public class RemoveItemCommandTest
{
    private IShopService _shopService;
    private IContext _context;
    private RemoveItemCommand _command;

    [SetUp]
    public void SetUp()
    {
        _shopService = Substitute.For<IShopService>();
        _context = Substitute.For<IContext>();
        _command = new RemoveItemCommand(_shopService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsWhitelistOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsWhitelistOnly, Is.True);
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeShopRemoveItemHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("shop_remove_item_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasNoParts()
    {
        // Arrange
        _context.Target.Returns("OnlyOnePart");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_remove_item_help");
        await _shopService.DidNotReceive().RemoveItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyItemNotInPalier_WhenRemoveReturnsFalse()
    {
        // Arrange
        _context.Target.Returns("1,Ghost Item");
        _shopService.RemoveItemAsync("1", "Ghost Item", Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_item_not_in_palier", "Ghost Item", "1");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyItemRemoved_WhenRemoveReturnsTrue()
    {
        // Arrange
        _context.Target.Returns("2,My Article");
        _shopService.RemoveItemAsync("2", "My Article", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_item_removed", "My Article", "2");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallRemoveItemWithTrimmedArgs_WhenTargetHasWhitespace()
    {
        // Arrange
        _context.Target.Returns("  1  ,  My Article  ");
        _shopService.RemoveItemAsync("1", "My Article", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _shopService.Received(1).RemoveItemAsync("1", "My Article", Arg.Any<CancellationToken>());
    }
}
