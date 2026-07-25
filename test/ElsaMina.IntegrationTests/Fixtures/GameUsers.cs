using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Fixtures;

/// <summary>
/// Player stand-ins for the card game characterization tests.
/// </summary>
public static class GameUsers
{
    public static IUser User(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return user;
    }

    /// <summary>
    /// <paramref name="count"/> users named <c>player1</c>..<c>playerN</c>, matching the naming the
    /// existing unit tests use for deterministic deals.
    /// </summary>
    public static IReadOnlyList<IUser> Players(int count) =>
        Enumerable.Range(1, count).Select(index => User($"player{index}")).ToList();
}
