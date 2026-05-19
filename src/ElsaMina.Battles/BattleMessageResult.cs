namespace ElsaMina.Battles;

public readonly record struct BattleMessageResult(BattleMessageType Type, string WinnerName = null, bool IsTie = false);
