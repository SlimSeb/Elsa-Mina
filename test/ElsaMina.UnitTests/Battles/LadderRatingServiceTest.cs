using ElsaMina.Battles;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.UserData;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Battles;

public class LadderRatingServiceTest
{
    private IUserDataService _userDataService;
    private LadderRatingService _service;

    [SetUp]
    public void SetUp()
    {
        _userDataService = Substitute.For<IUserDataService>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa-Mina");
        _service = new LadderRatingService(_userDataService, configuration);
    }

    [Test]
    public async Task Test_GetRatingsAsync_ShouldReturnBotsLatestRatings()
    {
        // Arrange
        var rating = new UserDataRankingDto { Elo = 1342 };
        _userDataService.GetLatestUserDataAsync("Elsa-Mina", Arg.Any<CancellationToken>()).Returns(new UserDataDto
        {
            Ratings = new Dictionary<string, UserDataRankingDto> { ["gen9randombattle"] = rating }
        });

        // Act
        var ratings = await _service.GetRatingsAsync();

        // Assert
        Assert.That(ratings["gen9randombattle"], Is.SameAs(rating));
    }

    [Test]
    public async Task Test_GetRatingsAsync_ShouldReturnNoRating_WhenBotHasNeverLaddered()
    {
        // Arrange
        _userDataService.GetLatestUserDataAsync("Elsa-Mina", Arg.Any<CancellationToken>())
            .Returns(new UserDataDto());

        // Act
        var ratings = await _service.GetRatingsAsync();

        // Assert
        Assert.That(ratings, Is.Empty);
    }

    [Test]
    public async Task Test_GetRatingsAsync_ShouldReturnNull_WhenShowdownDoesNotAnswer()
    {
        // Arrange
        _userDataService.GetLatestUserDataAsync("Elsa-Mina", Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var ratings = await _service.GetRatingsAsync();

        // Assert
        Assert.That(ratings, Is.Null);
    }
}
