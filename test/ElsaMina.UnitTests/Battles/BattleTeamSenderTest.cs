using ElsaMina.Battles;
using ElsaMina.Core;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles;

[TestFixture]
public class BattleTeamSenderTest
{
    private IBot _bot;
    private BattleTeamSender _teamSender;

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _teamSender = new BattleTeamSender(_bot);
    }

    [Test]
    public void Test_SendWithTeam_ShouldSetTeamThenSendCommandGlobally_WhenNoRoomIsGiven()
    {
        // Act
        _teamSender.SendWithTeam("packedteam", "/search gen9ou");

        // Assert
        Received.InOrder(() =>
        {
            _bot.Send("|/utm packedteam");
            _bot.Say(string.Empty, "/search gen9ou");
        });
    }

    [Test]
    public void Test_SendWithTeam_ShouldClearTeamThenSendCommandInRoom_WhenNoTeamAndRoomAreGiven()
    {
        // Act
        _teamSender.SendWithTeam(null, "/tour acceptchallenge", "randbats");

        // Assert
        Received.InOrder(() =>
        {
            _bot.Send("|/utm null");
            _bot.Say("randbats", "/tour acceptchallenge");
        });
    }
}
