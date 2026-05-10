using ElsaMina.Commands.Profile;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

// GetAvatar tests are in ProfileServiceTest

namespace ElsaMina.UnitTests.Commands.Profile;

public class ProfileCommandTest
{
    private IProfileService _profileService;
    private ProfileCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _profileService = Substitute.For<IProfileService>();
        _sut = new ProfileCommand(_profileService);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseSenderUserId_WhenTargetIsEmpty()
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns("");
        context.Sender.UserId.Returns("alice");
        context.RoomId.Returns("room1");
        context.HasRankOrHigher(Rank.Voiced).Returns(true);
        _profileService.GetProfileHtmlAsync("alice", "room1", Arg.Any<CancellationToken>())
            .Returns("rendered");

        await _sut.RunAsync(context);

        context.Received().SendUpdatableHtml("profile-alice", Arg.Any<string>(), isChanging: false);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTarget_WhenProvided()
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns("Bob99");
        context.RoomId.Returns("room1");
        context.HasRankOrHigher(Rank.Voiced).Returns(true);
        _profileService.GetProfileHtmlAsync("bob99", "room1", Arg.Any<CancellationToken>())
            .Returns("rendered");

        await _sut.RunAsync(context);

        context.Received().SendUpdatableHtml("profile-bob99", Arg.Any<string>(), isChanging: false);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenNoRoomProvided()
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns("alice");
        context.RoomId.Returns("currentroom");
        _profileService.GetProfileHtmlAsync("alice", "currentroom", Arg.Any<CancellationToken>())
            .Returns("rendered");

        await _sut.RunAsync(context);

        await _profileService.Received(1).GetProfileHtmlAsync("alice", "currentroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseProvidedRoomId_WhenSecondParameterGiven()
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns("alice, otherRoom");
        context.RoomId.Returns("currentroom");
        _profileService.GetProfileHtmlAsync("alice", "otherroom", Arg.Any<CancellationToken>())
            .Returns("rendered");

        await _sut.RunAsync(context);

        await _profileService.Received(1).GetProfileHtmlAsync("alice", "otherroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseSenderWithProvidedRoom_WhenOnlyRoomGiven()
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(", otherroom");
        context.Sender.UserId.Returns("alice");
        context.RoomId.Returns("currentroom");
        _profileService.GetProfileHtmlAsync("alice", "otherroom", Arg.Any<CancellationToken>())
            .Returns("rendered");

        await _sut.RunAsync(context);

        await _profileService.Received(1).GetProfileHtmlAsync("alice", "otherroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotReply_WhenUserIdIsNull()
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns((string)null);
        context.Sender.UserId.Returns((string)null);

        await _sut.RunAsync(context);

        context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }
}
