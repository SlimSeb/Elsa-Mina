namespace ElsaMina.Battles;

public interface IBattleMessageParser
{
    bool TryApplyMessage(string[] parts, string roomId, BattleContext context, out BattleMessageResult result);
}
