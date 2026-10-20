using ElsaMina.Commands.Games.Semantix;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Semantix;

public class SemantixAnswerCommandTest
{
    private ISemantixDailyService _dailyService;
    private SemantixAnswerCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dailyService = Substitute.For<ISemantixDailyService>();
        _command = new SemantixAnswerCommand(_dailyService);
    }

    [Test]
    public void Test_Properties_ShouldBeHiddenAndWhitelistOnly()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.IsWhitelistOnly, Is.True);
            Assert.That(_command.IsHidden, Is.True);
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldPrivatelySendTodaysAnswer()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("dev");
        context.Sender.Returns(sender);
        _dailyService.GetDailyAnswer().Returns("pomme");
        _dailyService.Today.Returns(new DateOnly(2026, 6, 15));

        // Act
        await _command.RunAsync(context);

        // Assert
        context.Received(1).Reply("/pm dev, Semantix (2026-06-15): pomme");
    }
}
