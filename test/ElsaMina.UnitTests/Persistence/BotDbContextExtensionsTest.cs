using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ElsaMina.UnitTests.Persistence;

public class BotDbContextExtensionsTest
{
    private DbContextOptions<BotDbContext> _options = null!;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [Test]
    public async Task Test_EnsureUserExistsAsync_ShouldCreateStubUser_WhenUserDoesNotExist()
    {
        // Arrange
        await using var dbContext = new BotDbContext(_options);

        // Act
        await dbContext.EnsureUserExistsAsync("ghost");
        await dbContext.SaveChangesAsync();

        // Assert
        var user = await dbContext.Users.FindAsync("ghost");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(user, Is.Not.Null);
            Assert.That(user!.UserName, Is.EqualTo("ghost"));
        }
    }

    [Test]
    public async Task Test_EnsureUserExistsAsync_ShouldNotDuplicateOrOverwrite_WhenUserAlreadyExists()
    {
        // Arrange
        await using (var seedContext = new BotDbContext(_options))
        {
            seedContext.Users.Add(new SavedUser { UserId = "existing", UserName = "Existing Name" });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = new BotDbContext(_options);

        // Act
        await dbContext.EnsureUserExistsAsync("existing");
        await dbContext.SaveChangesAsync();

        // Assert
        var users = await dbContext.Users.Where(user => user.UserId == "existing").ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(users, Has.Count.EqualTo(1));
            Assert.That(users[0].UserName, Is.EqualTo("Existing Name"));
        }
    }

    [Test]
    public async Task Test_EnsureUserExistsAsync_ShouldDoNothing_WhenUserIdIsNullOrEmpty()
    {
        // Arrange
        await using var dbContext = new BotDbContext(_options);

        // Act
        await dbContext.EnsureUserExistsAsync(null!);
        await dbContext.EnsureUserExistsAsync(string.Empty);
        await dbContext.SaveChangesAsync();

        // Assert
        Assert.That(await dbContext.Users.CountAsync(), Is.Zero);
    }
}
