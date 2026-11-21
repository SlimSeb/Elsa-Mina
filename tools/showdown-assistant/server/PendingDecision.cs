using ElsaMina.Battles.Strategies.MiniPoke;

namespace ElsaMina.BattleAssistant;

/// <summary>
/// The decision a room is waiting for, as the model saw it: what the search starts from.
/// </summary>
/// <param name="Rqid">The request.</param>
/// <param name="LineCount">The room's lines when it came: the search replays them into its own context.</param>
/// <param name="Observation">The encoded position.</param>
/// <param name="Logits">The model's logits.</param>
/// <param name="Allowed">The legal actions the guard allows and the server could describe.</param>
public sealed record PendingDecision(
    int Rqid,
    int LineCount,
    BattleObservation Observation,
    float[] Logits,
    bool[] Allowed);
