using ElsaMina.Commands.Shop;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Shop;

[TestFixture]
public class EditItemCommandTest
{
    private IShopService _shopService;
    private IContext _context;
    private EditItemCommand _command;

    [SetUp]
    public void SetUp()
    {
        _shopService = Substitute.For<IShopService>();
        _context = Substitute.For<IContext>();
        _command = new EditItemCommand(_shopService);
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
    public void Test_HelpMessageKey_ShouldBeShopEditItemHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("shop_edit_item_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasFewerThanFourParts()
    {
        // Arrange
        _context.Target.Returns("1,ignored,New Name");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_edit_item_help");
        await _shopService.DidNotReceive().UpdateItemAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidId_WhenIdIsNotAnInteger()
    {
        // Arrange
        _context.Target.Returns("notanumber,ignored,New Name,999");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_invalid_id");
        await _shopService.DidNotReceive().UpdateItemAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyItemNotFound_WhenUpdateReturnsNull()
    {
        // Arrange
        _context.Target.Returns("42,ignored,New Name,999");
        _shopService.UpdateItemAsync(42, "New Name", "999", Arg.Any<CancellationToken>())
            .Returns((ShopItem)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_item_not_found", 42);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyItemEdited_WhenUpdateSucceeds()
    {
        // Arrange
        _context.Target.Returns("42,ignored,New Name,999");
        _shopService.UpdateItemAsync(42, "New Name", "999", Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 42, Article = "New Name", Price = "999" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_item_edited", 42);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallUpdateWithTrimmedArgs_WhenTargetHasWhitespace()
    {
        // Arrange
        _context.Target.Returns("  42  ,  ignored  ,  New Name  ,  999  ");
        _shopService.UpdateItemAsync(42, "New Name", "999", Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 42, Article = "New Name", Price = "999" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _shopService.Received(1).UpdateItemAsync(42, "New Name", "999", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldIgnoreSecondArg_WhenBuildingUpdateCall()
    {
        // Arrange - args[1] is skipped by the command; only args[0], args[2], args[3] are used
        _context.Target.Returns("42,this-is-ignored,New Name,999");
        _shopService.UpdateItemAsync(42, "New Name", "999", Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 42, Article = "New Name", Price = "999" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _shopService.Received(1).UpdateItemAsync(42, "New Name", "999", Arg.Any<CancellationToken>());
    }
}
