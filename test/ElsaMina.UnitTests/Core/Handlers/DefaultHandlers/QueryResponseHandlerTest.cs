using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.BattleTracker;
using ElsaMina.Core.Services.RoomInfo;
using ElsaMina.Core.Services.UserDetails;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Handlers.DefaultHandlers;

public class QueryResponseHandlerTest
{
    private IUserDetailsManager _userDetailsManager;
    private IActiveBattlesManager _activeBattlesManager;
    private IRoomInfoManager _roomInfoManager;
    private QueryResponseHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _userDetailsManager = Substitute.For<IUserDetailsManager>();
        _activeBattlesManager = Substitute.For<IActiveBattlesManager>();
        _roomInfoManager = Substitute.For<IRoomInfoManager>();
        _handler = new QueryResponseHandler(_userDetailsManager, _activeBattlesManager, _roomInfoManager);
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldCallHandleReceivedRoomList_WhenCommandIsQueryResponseRoomList()
    {
        // Arrange
        string[] parts = ["", "queryresponse", "roomlist", "roomListData"];

        // Act
        await _handler.HandleReceivedMessageAsync(parts);

        // Assert
        _activeBattlesManager.Received(1).HandleReceivedRoomList("roomListData");
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldCallHandleReceivedUserDetails_WhenCommandIsQueryResponseUserDetails()
    {
        // Arrange
        string[] parts = ["", "queryresponse", "userdetails", "userDetailsData"];

        // Act
        await _handler.HandleReceivedMessageAsync(parts);

        // Assert
        _userDetailsManager.Received(1).HandleReceivedUserDetails("userDetailsData");
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldCallHandleReceivedRoomInfo_WhenCommandIsQueryResponseRoomInfo()
    {
        // Arrange
        string[] parts = ["", "queryresponse", "roominfo", "roomInfoData"];

        // Act
        await _handler.HandleReceivedMessageAsync(parts);

        // Assert
        _roomInfoManager.Received(1).HandleReceivedRoomInfo("roomInfoData");
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldNotCallHandleReceivedUserDetails_WhenPartsLengthIsLessThanFour()
    {
        // Arrange
        string[] parts = ["", "queryresponse", "userdetails"];

        // Act
        await _handler.HandleReceivedMessageAsync(parts);

        // Assert
        _userDetailsManager.DidNotReceive().HandleReceivedUserDetails(Arg.Any<string>());
        _activeBattlesManager.DidNotReceive().HandleReceivedRoomList(Arg.Any<string>());
        _roomInfoManager.DidNotReceive().HandleReceivedRoomInfo(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldNotCallHandleReceivedUserDetails_WhenSecondPartIsNotQueryResponse()
    {
        // Arrange
        string[] parts = ["", "notqueryresponse", "userdetails", "userDetailsData"];

        // Act
        await _handler.HandleReceivedMessageAsync(parts);

        // Assert
        _userDetailsManager.DidNotReceive().HandleReceivedUserDetails(Arg.Any<string>());
        _activeBattlesManager.DidNotReceive().HandleReceivedRoomList(Arg.Any<string>());
        _roomInfoManager.DidNotReceive().HandleReceivedRoomInfo(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleReceivedMessage_ShouldNotCallHandleReceivedUserDetails_WhenThirdPartIsNotUserDetails()
    {
        // Arrange
        string[] parts = ["", "queryresponse", "notuserdetails", "userDetailsData"];

        // Act
        await _handler.HandleReceivedMessageAsync(parts);

        // Assert
        _userDetailsManager.DidNotReceive().HandleReceivedUserDetails(Arg.Any<string>());
        _activeBattlesManager.DidNotReceive().HandleReceivedRoomList(Arg.Any<string>());
        _roomInfoManager.DidNotReceive().HandleReceivedRoomInfo(Arg.Any<string>());
    }
}
