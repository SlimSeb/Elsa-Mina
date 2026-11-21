using ElsaMina.Battles;
using ElsaMina.Battles.Challenges;
using ElsaMina.Core;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Teams;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Challenges;

public class ChallengeServiceTest
{
    private const string PIKACHU_EXPORT = "Pikachu @ Light Ball\nAbility: Static\n- Thunderbolt";
    private const string EEVEE_EXPORT = "Eevee @ Eviolite\nAbility: Adaptability\n- Return";

    private IBot _bot;
    private IBattleTeamSender _teamSender;
    private IBattleTeamsService _battleTeamsService;
    private IFormatsManager _formatsManager;
    private IBotDbContextFactory _dbContextFactory;
    private ITeamValidator _teamValidator;
    private IRandomService _randomService;
    private DbContextOptions<BotDbContext> _dbOptions;
    private ChallengeService _challengeService;

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _teamSender = Substitute.For<IBattleTeamSender>();
        _battleTeamsService = Substitute.For<IBattleTeamsService>();
        _formatsManager = Substitute.For<IFormatsManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _teamValidator = Substitute.For<ITeamValidator>();
        _randomService = Substitute.For<IRandomService>();
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));
        _teamValidator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _challengeService = new ChallengeService(_bot, _teamSender, _battleTeamsService, _formatsManager,
            _dbContextFactory, _teamValidator, _randomService);
    }

    [TearDown]
    public async Task TearDown()
    {
        _challengeService.Dispose();
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task Test_HandleChallengeAsync_ShouldAcceptWithTeamSavedWithoutGeneration_WhenFormatIsCurrentGeneration()
    {
        // Arrange
        await SaveTeamsAsync(("pikachu", "ou", PIKACHU_EXPORT));

        // Act
        await _challengeService.HandleChallengeAsync("challenger", "gen9ou");

        // Assert
        _teamSender.Received(1).SendWithTeam(Pack(PIKACHU_EXPORT), "/accept challenger");
    }

    [Test]
    public async Task Test_HandleChallengeAsync_ShouldPreferTeamSavedUnderFormatId_WhenTeamsAreSavedUnderBothNames()
    {
        // Arrange
        await SaveTeamsAsync(("pikachu", "ou", PIKACHU_EXPORT), ("eevee", "gen9ou", EEVEE_EXPORT));

        // Act
        await _challengeService.HandleChallengeAsync("challenger", "gen9ou");

        // Assert
        _teamSender.Received(1).SendWithTeam(Pack(EEVEE_EXPORT), "/accept challenger");
    }

    [Test]
    public async Task Test_HandleChallengeAsync_ShouldTryTeamSavedWithoutGeneration_WhenTeamSavedUnderFormatIdIsInvalid()
    {
        // Arrange
        await SaveTeamsAsync(("pikachu", "ou", PIKACHU_EXPORT), ("eevee", "gen9ou", EEVEE_EXPORT));
        _teamValidator.ValidateAsync(Pack(EEVEE_EXPORT), "gen9ou", Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _challengeService.HandleChallengeAsync("challenger", "gen9ou");

        // Assert
        _teamSender.Received(1).SendWithTeam(Pack(PIKACHU_EXPORT), "/accept challenger");
    }

    [Test]
    public async Task Test_HandleChallengeAsync_ShouldReject_WhenOnlyTeamWithoutGenerationIsSavedAndFormatIsPastGeneration()
    {
        // Arrange
        await SaveTeamsAsync(("pikachu", "ou", PIKACHU_EXPORT));
        _formatsManager.GetCleanFormat("gen8ou").Returns("[Gen 8] OU");

        // Act
        await _challengeService.HandleChallengeAsync("challenger", "gen8ou");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            _teamSender.DidNotReceive().SendWithTeam(Arg.Any<string>(), Arg.Any<string>());
            _bot.Received(1).Send("|/reject challenger");
            _bot.Received(1).Send("|/pm challenger, Sorry, I have no valid team for [Gen 8] OU.");
        }
    }

    private async Task SaveTeamsAsync(params (string Id, string Format, string Export)[] teams)
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        foreach (var (id, format, export) in teams)
        {
            await dbContext.Teams.AddAsync(new Team
            {
                Id = id,
                Name = id,
                Format = format,
                TeamJson = ShowdownTeamsUtils.TeamExportToJson(export)
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static string Pack(string export) =>
        ShowdownTeamsUtils.PackTeam(ShowdownTeamsUtils.DeserializeTeamExport(export));
}
