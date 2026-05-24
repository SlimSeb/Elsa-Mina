using System.Globalization;
using ElsaMina.Commands.Shop;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Shop;

[TestFixture]
public class DisplayShopCommandTest
{
    private IShopService _shopService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private DisplayShopCommand _command;

    [SetUp]
    public void SetUp()
    {
        _shopService = Substitute.For<IShopService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaBot");
        _context.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _shopService.GetShopDataAsync().Returns(new Dictionary<string, List<ShopItem>>());
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");

        _command = new DisplayShopCommand(_shopService, _templatesManager, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_IsWhitelistOnly_ShouldBeFalse()
    {
        Assert.That(_command.IsWhitelistOnly, Is.False);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeShopDisplayHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("shop_display_help"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallGetShopData_WhenExecuted()
    {
        // Act
        await _command.RunAsync(_context);

        // Assert
        await _shopService.Received(1).GetShopDataAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderShopTableTemplate_WhenExecuted()
    {
        // Arrange
        var shopData = new Dictionary<string, List<ShopItem>>
        {
            ["1"] = [new ShopItem { Id = 1, Tier = "1", Article = "Item A", Price = "100" }]
        };
        _shopService.GetShopDataAsync().Returns(shopData);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Shop/ShopTable",
            Arg.Is<ShopViewModel>(vm =>
                vm.Items == shopData &&
                vm.BotName == "ElsaBot"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallReplyHtml_WhenExecuted()
    {
        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCultureToViewModel_WhenExecuted()
    {
        // Arrange
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        _context.Culture.Returns(culture);

        ShopViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<ShopViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        Assert.That(capturedViewModel, Is.Not.Null);
        Assert.That(capturedViewModel.Culture, Is.EqualTo(culture));
    }
}
