using ElsaMina.Commands.Shop;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Shop;

[TestFixture]
public class AddItemCommandTest
{
    private IShopService _shopService;
    private IContext _context;
    private AddItemCommand _command;

    [SetUp]
    public void SetUp()
    {
        _shopService = Substitute.For<IShopService>();
        _context = Substitute.For<IContext>();
        _command = new AddItemCommand(_shopService);
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
    public void Test_HelpMessageKey_ShouldBeShopAddItemHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("shop_add_item_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasFewerThanThreeParts()
    {
        // Arrange
        _context.Target.Returns("Article,100");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_add_item_help");
        await _shopService.DidNotReceive().AddItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelp_WhenTargetHasOnlyOneComma()
    {
        // Arrange
        _context.Target.Returns("Article");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_add_item_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPalier_WhenPalierIsNotValid()
    {
        // Arrange
        _context.Target.Returns("My Article,100,5");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_invalid_palier");
        await _shopService.DidNotReceive().AddItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [TestCase("1")]
    [TestCase("2")]
    [TestCase("3")]
    [TestCase("4")]
    public async Task Test_RunAsync_ShouldCallAddItem_WhenPalierIsValid(string palier)
    {
        // Arrange
        _context.Target.Returns($"My Article,100,{palier}");
        _shopService.AddItemAsync(palier, "My Article", "100", Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 1, Tier = palier, Article = "My Article", Price = "100" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _shopService.Received(1).AddItemAsync(palier, "My Article", "100", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyItemAdded_WhenAddSucceeds()
    {
        // Arrange
        _context.Target.Returns("My Article,100,2");
        _shopService.AddItemAsync("2", "My Article", "100", Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 42, Tier = "2", Article = "My Article", Price = "100" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_item_added", "My Article", "2", "100", 42);
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimArgs_WhenTargetHasExtraWhitespace()
    {
        // Arrange
        _context.Target.Returns("  My Article  ,  100  ,  1  ");
        _shopService.AddItemAsync("1", "My Article", "100", Arg.Any<CancellationToken>())
            .Returns(new ShopItem { Id = 1, Tier = "1", Article = "My Article", Price = "100" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _shopService.Received(1).AddItemAsync("1", "My Article", "100", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidPalier_WhenExtraCommasAreInTarget()
    {
        // Arrange - split(',', 3) caps at 3 parts, so "Article,100,1,extra" yields palier = "1,extra" which is invalid
        _context.Target.Returns("Article,100,1,extra");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("shop_invalid_palier");
        await _shopService.DidNotReceive().AddItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
