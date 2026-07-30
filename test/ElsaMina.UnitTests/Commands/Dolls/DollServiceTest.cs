using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Sheets.GoogleDrive;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Dolls;

[TestFixture]
public class DollServiceTest
{
    private const string DRIVE_ID = "drive-poupees";

    private DbContextOptions<BotDbContext> _options;
    private IBotDbContextFactory _dbContextFactory;
    private IDriveProvider _driveProvider;
    private IConfiguration _configuration;
    private IClockService _clockService;
    private DateTime _now;
    private DollService _sut;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));

        _driveProvider = Substitute.For<IDriveProvider>();
        _configuration = Substitute.For<IConfiguration>();
        _configuration.DollsDriveName.Returns("Poupées");

        _now = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTime.Returns(_ => _now);

        _sut = new DollService(_driveProvider, _dbContextFactory, _configuration, _clockService);
    }

    private void SetUpDrive(params (string FolderName, string[] FileNames)[] folders)
    {
        // The name resolves to a folder shared with the bot, which is the common setup.
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>()).Returns(DRIVE_ID);
        _driveProvider.ListChildrenAsync(DRIVE_ID, GoogleDriveProvider.FOLDER_MIME_TYPE, Arg.Any<CancellationToken>())
            .Returns(folders
                .Select((folder, index) => new DriveItem($"folder-{index}", folder.FolderName))
                .ToList());

        for (var index = 0; index < folders.Length; index++)
        {
            var fileNames = folders[index].FileNames;
            _driveProvider.ListChildrenAsync($"folder-{index}", "image/png", Arg.Any<CancellationToken>())
                .Returns(fileNames.Select(fileName => new DriveItem($"file-{fileName}", fileName)).ToList());
        }
    }

    private async Task SeedHoldingAsync(string dollId, string roomId, string userId)
    {
        await using var dbContext = new BotDbContext(_options);
        dbContext.DollHoldings.Add(new DollHolding { DollId = dollId, RoomId = roomId, UserId = userId });
        await dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldBuildDollsFromTheFolderSizesAndFileNames()
    {
        // Arrange
        SetUpDrive(
            ("Petites 16x16", ["pikachu_16x16.png"]),
            ("Grandes 32x32", ["riolu_debout_face_deux_pieds_32x32.png"]));

        // Act
        var catalogue = await _sut.GetCatalogueAsync();

        // Assert
        Assert.That(catalogue.Keys, Is.EquivalentTo(new[] { "pikachu", "rioludeboutfacedeuxpieds" }));
        Assert.Multiple(() =>
        {
            Assert.That(catalogue["pikachu"].Size, Is.EqualTo(16));
            Assert.That(catalogue["pikachu"].Name, Is.EqualTo("Pikachu"));
            Assert.That(catalogue["pikachu"].Image,
                Is.EqualTo("https://lh3.googleusercontent.com/d/file-pikachu_16x16.png"));
            Assert.That(catalogue["rioludeboutfacedeuxpieds"].Size, Is.EqualTo(32));
            Assert.That(catalogue["rioludeboutfacedeuxpieds"].Name, Is.EqualTo("Riolu debout face deux pieds"));
        });
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldSkipFoldersWithoutASizeInTheirName()
    {
        // Arrange
        SetUpDrive(
            ("Moyennes 24x24", ["clefairy.png"]),
            ("Brouillons", ["wip.png"]));

        // Act
        var catalogue = await _sut.GetCatalogueAsync();

        // Assert
        Assert.That(catalogue.Keys, Is.EquivalentTo(new[] { "clefairy" }));
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldFallBackToASharedDrive_WhenNoFolderMatches()
    {
        // Arrange
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>()).Returns((string)null);
        _driveProvider.FindSharedDriveIdAsync("Poupées", Arg.Any<CancellationToken>()).Returns("shared-drive-root");
        _driveProvider
            .ListChildrenAsync("shared-drive-root", GoogleDriveProvider.FOLDER_MIME_TYPE, Arg.Any<CancellationToken>())
            .Returns([new DriveItem("folder-0", "Petites 16x16")]);
        _driveProvider.ListChildrenAsync("folder-0", "image/png", Arg.Any<CancellationToken>())
            .Returns([new DriveItem("file-0", "pikachu.png")]);

        // Act
        var catalogue = await _sut.GetCatalogueAsync();

        // Assert
        Assert.That(catalogue.Keys, Is.EquivalentTo(new[] { "pikachu" }));
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldUseAConfiguredIdDirectly_WithoutAnyLookup()
    {
        // Arrange
        const string folderId = "1AbCdEfGhIjKlMnOpQrStUvWxYz0123456";
        _configuration.DollsDriveName.Returns(folderId);
        _driveProvider.ListChildrenAsync(folderId, GoogleDriveProvider.FOLDER_MIME_TYPE, Arg.Any<CancellationToken>())
            .Returns([new DriveItem("folder-0", "Grandes 32x32")]);
        _driveProvider.ListChildrenAsync("folder-0", "image/png", Arg.Any<CancellationToken>())
            .Returns([new DriveItem("file-0", "snorlax.png")]);

        // Act
        var catalogue = await _sut.GetCatalogueAsync();

        // Assert
        Assert.That(catalogue.Keys, Is.EquivalentTo(new[] { "snorlax" }));
        await _driveProvider.DidNotReceiveWithAnyArgs().FindFolderIdAsync(default);
        await _driveProvider.DidNotReceiveWithAnyArgs().FindSharedDriveIdAsync(default);
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldStillUseTheFolder_WhenListingSharedDrivesIsForbidden()
    {
        // Arrange: a service account that is not a member of any shared drive gets an error there.
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>()).Returns("folder-root");
        _driveProvider.FindSharedDriveIdAsync("Poupées", Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("403 forbidden"));
        _driveProvider
            .ListChildrenAsync("folder-root", GoogleDriveProvider.FOLDER_MIME_TYPE, Arg.Any<CancellationToken>())
            .Returns([new DriveItem("folder-0", "Petites 16x16")]);
        _driveProvider.ListChildrenAsync("folder-0", "image/png", Arg.Any<CancellationToken>())
            .Returns([new DriveItem("file-0", "pikachu.png")]);

        // Act
        var catalogue = await _sut.GetCatalogueAsync();

        // Assert
        Assert.That(catalogue.Keys, Is.EquivalentTo(new[] { "pikachu" }));
    }

    [Test]
    public void Test_GetCatalogueAsync_ShouldThrow_WhenListingSharedDrivesFailsAndNoFolderMatches()
    {
        // Arrange
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>()).Returns((string)null);
        _driveProvider.FindSharedDriveIdAsync("Poupées", Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("403 forbidden"));

        // Act & Assert
        Assert.ThatAsync(() => _sut.GetCatalogueAsync(), Throws.InvalidOperationException);
    }

    [Test]
    public void Test_GetCatalogueAsync_ShouldThrow_WhenNeitherDriveNorFolderExists()
    {
        // Arrange
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>()).Returns((string)null);
        _driveProvider.FindSharedDriveIdAsync("Poupées", Arg.Any<CancellationToken>()).Returns((string)null);

        // Act & Assert
        Assert.ThatAsync(() => _sut.GetCatalogueAsync(), Throws.InvalidOperationException);
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldCacheTheCatalogue()
    {
        // Arrange
        SetUpDrive(("Petites 16x16", ["pikachu.png"]));
        await _sut.GetCatalogueAsync();

        // Act
        await _sut.GetCatalogueAsync();

        // Assert
        await _driveProvider.Received(1).FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldReadTheDriveAgain_WhenTheCacheHasExpired()
    {
        // Arrange
        SetUpDrive(("Petites 16x16", ["pikachu.png"]));
        await _sut.GetCatalogueAsync();
        _now = _now.AddMinutes(31);

        // Act
        await _sut.GetCatalogueAsync();

        // Assert
        await _driveProvider.Received(2).FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RefreshCatalogueAsync_ShouldAlwaysReadTheDriveAgain()
    {
        // Arrange
        SetUpDrive(("Petites 16x16", ["pikachu.png"]));
        await _sut.GetCatalogueAsync();

        // Act
        var catalogue = await _sut.RefreshCatalogueAsync();

        // Assert
        Assert.That(catalogue.Keys, Is.EquivalentTo(new[] { "pikachu" }));
        await _driveProvider.Received(2).FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldNotCacheFailures()
    {
        // Arrange
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("drive down"));

        // Act
        Assert.ThatAsync(() => _sut.GetCatalogueAsync(), Throws.TypeOf<HttpRequestException>());
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>()).Returns(DRIVE_ID);
        _driveProvider.ListChildrenAsync(DRIVE_ID, GoogleDriveProvider.FOLDER_MIME_TYPE, Arg.Any<CancellationToken>())
            .Returns([]);

        // Assert
        Assert.That(await _sut.GetCatalogueAsync(), Is.Empty);
    }

    [Test]
    public async Task Test_GetDollAsync_ShouldReturnNull_WhenTheDollIsNotInTheCatalogue()
    {
        // Arrange
        SetUpDrive(("Petites 16x16", ["pikachu.png"]));

        // Act & Assert
        Assert.That(await _sut.GetDollAsync("unknown"), Is.Null);
        Assert.That(await _sut.GetDollAsync("pikachu"), Is.Not.Null);
    }

    [Test]
    public async Task Test_ResolveDollsAsync_ShouldOrderByDescendingSizeThenName_AndSkipUnknownIds()
    {
        // Arrange
        SetUpDrive(
            ("Petites 16x16", ["pikachu.png"]),
            ("Grandes 32x32", ["snorlax.png", "clefairy.png"]));

        // Act
        var dolls = await _sut.ResolveDollsAsync(["pikachu", "snorlax", "clefairy", "deleted_from_drive"]);

        // Assert
        Assert.That(dolls.Select(doll => doll.Id), Is.EqualTo(new[] { "clefairy", "snorlax", "pikachu" }));
    }

    [Test]
    public async Task Test_ResolveDollsAsync_ShouldReturnEmpty_WhenTheDriveCannotBeRead()
    {
        // Arrange
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("drive down"));

        // Act
        var dolls = await _sut.ResolveDollsAsync(["pikachu"]);

        // Assert
        Assert.That(dolls, Is.Empty);
    }

    [Test]
    public async Task Test_IsDollOwnedByUserAsync_ShouldBeScopedToTheRoomAndTheUser()
    {
        // Arrange
        await SeedHoldingAsync("pikachu", "room1", "alice");

        // Act & Assert
        Assert.That(await _sut.IsDollOwnedByUserAsync("room1", "alice", "pikachu"), Is.True);
        Assert.That(await _sut.IsDollOwnedByUserAsync("room2", "alice", "pikachu"), Is.False);
        Assert.That(await _sut.IsDollOwnedByUserAsync("room1", "bob", "pikachu"), Is.False);
    }

    [Test]
    public async Task Test_GetCatalogueAsync_ShouldUseTheDefaultDriveName_WhenNotConfigured()
    {
        // Arrange
        _configuration.DollsDriveName.Returns(string.Empty);
        _driveProvider.FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>()).Returns(DRIVE_ID);
        _driveProvider.ListChildrenAsync(DRIVE_ID, GoogleDriveProvider.FOLDER_MIME_TYPE, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await _sut.GetCatalogueAsync();

        // Assert
        await _driveProvider.Received(1).FindFolderIdAsync("Poupées", null, Arg.Any<CancellationToken>());
    }
}
