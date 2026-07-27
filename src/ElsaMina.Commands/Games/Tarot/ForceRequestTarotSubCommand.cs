using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Tarot;

/// <summary>
/// Lets staff put another player up for substitution without that player having to ask. Triggered in the
/// room (<c>tarotforcesub playerid</c>) or in a private message whose target is prefixed with the room id
/// (<c>roomid, playerid</c>).
/// </summary>
[NamedCommand("tarotforcesub", Aliases = ["tarotsubout", "tfs"])]
public class ForceRequestTarotSubCommand : TargetedSubCommandBase<ITarotGame>
{
    public ForceRequestTarotSubCommand(IRoomsManager roomsManager) : base(roomsManager)
    {
    }

    protected override string ResourcePrefix => "tarot";
    public override Rank RequiredRank => Rank.Driver;

    protected override Task<(bool Success, string MessageKey, object[] Args)> ExecuteAsync(ITarotGame game,
        IContext context, string targetPlayerId) => game.ForceRequestSubAsync(targetPlayerId);
}
