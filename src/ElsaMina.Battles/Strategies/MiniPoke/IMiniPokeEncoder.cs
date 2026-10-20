namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Turns ElsaMina's battle state into a minipoke model's input tensors.
///
/// Each model version has its own encoder, which MUST reproduce the encoder.py that version was trained with,
/// feature for feature (same vocab ids, same one-hot orderings, same scaling). Any mismatch is silent: the model
/// still returns legal moves, it just plays worse. Each version's parity test checks it against battles recorded
/// by tools/minipoke/make_fixtures.py.
///
/// It reads <see cref="BattleContext.Tracked"/>, which holds all the per-battle state (including the stable team
/// order), so it keeps none itself.
/// </summary>
public interface IMiniPokeEncoder
{
    /// <summary>Returns null when the state can't be encoded (e.g. not a singles battle).</summary>
    BattleObservation Encode(BattleContext context);
}
