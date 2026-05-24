using ElsaMina.Commands.Misc.Wiki;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Wiki;

[TestFixture]
public class BulbapediaSearchCommandTest
{
    private BulbapediaSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _command = new BulbapediaSearchCommand(Substitute.For<IHttpService>());
    }

    [Test]
    public void Test_Constructor_ShouldSetProperties()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
        }
    }
}
