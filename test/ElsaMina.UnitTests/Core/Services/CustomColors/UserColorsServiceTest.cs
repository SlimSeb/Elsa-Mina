using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Utils;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.CustomColors;

public class UserColorsServiceTest
{
    private ICustomColorsManager _customColorsManager;
    private IRoomColorsCache _roomColorsCache;
    private UserColorsService _userColorsService;

    [SetUp]
    public void SetUp()
    {
        _customColorsManager = Substitute.For<ICustomColorsManager>();
        _customColorsManager.CustomColorsMapping.Returns(new Dictionary<string, string>());
        _roomColorsCache = Substitute.For<IRoomColorsCache>();
        _roomColorsCache.GetColor(Arg.Any<string>()).Returns((string)null);

        _userColorsService = new UserColorsService(_roomColorsCache, _customColorsManager);
    }

    [Test]
    public void Test_GetUserColor_ShouldUseNameColor_WhenNameColorCacheHasEntry()
    {
        _roomColorsCache.GetColor("customuser").Returns("#abcdef");

        var result = _userColorsService.GetUserColor("customUser");

        Assert.That(result, Is.EqualTo("#abcdef"));
    }

    [Test]
    public void Test_GetUserColor_ShouldUseCustomColor_WhenCustomColorExists()
    {
        // Arrange
        var userName = "customUser";
        var customColorUsername = "speks";
        _customColorsManager.CustomColorsMapping
            .Returns(new Dictionary<string, string> { { userName.ToLowerAlphaNum(), customColorUsername } });

        // Act
        var result = _userColorsService.GetUserColor(userName);

        // Assert
        Assert.That(result, Is.EqualTo(customColorUsername.ToColor().ToHexString()));
    }

    [Test]
    public void Test_GetUserColor_ShouldFallbackToGeneratedColor_WhenNoCustomColorExists()
    {
        var result = _userColorsService.GetUserColor("fallbackUser");

        Assert.That(result, Is.EqualTo("fallbackUser".ToColor().ToHexString()));
    }
}
