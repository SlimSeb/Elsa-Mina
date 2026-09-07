using ElsaMina.Commands.Development;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Utils;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development;

[TestFixture]
public class MemoryUsageCommandTest
{
    private IContext _context;
    private ISystemService _systemService;
    private MemoryUsageCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _systemService = Substitute.For<ISystemService>();
        _command = new MemoryUsageCommand(_systemService);
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
    public void Test_NamedCommandAttribute_ShouldHaveSystemInfoAlias()
    {
        var attribute = typeof(MemoryUsageCommand).GetCommandAttribute();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute.Name, Is.EqualTo("memusage"));
        Assert.That(attribute.Aliases, Does.Contain("systeminfo"));
        Assert.That(attribute.Aliases, Does.Contain("memoryusage"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithSystemInfo()
    {
        var systemInfo = new SystemInfo
        {
            FrameworkDescription = ".NET 10.0.0",
            RuntimeIdentifier = "osx-arm64",
            OsDescription = "macOS 15.3.1",
            WorkingSet = 50 * 1024 * 1024
        };
        _systemService.GetSystemInfo().Returns(systemInfo);

        await _command.RunAsync(_context);

        _context.Received(1).Reply($"!code {systemInfo}");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotReply_WhenSystemInfoIsNull()
    {
        _systemService.GetSystemInfo().Returns((SystemInfo)null);

        await _command.RunAsync(_context);

        _context.DidNotReceive().Reply(Arg.Any<string>());
    }
}
