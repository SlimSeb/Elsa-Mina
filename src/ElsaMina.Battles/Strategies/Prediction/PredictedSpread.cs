namespace ElsaMina.Battles.Strategies.Prediction;

/// <summary>
/// A nature + EV spread the opponent's pokemon is expected to run, derived from Smogon usage stats.
/// Applying it (rather than the calc's default 0-EV neutral spread) is what makes speed tiers,
/// damage and bulk realistic: a competitive Dragapult runs 421 Speed, not the default 320.
/// </summary>
public record PredictedSpread(
    string Nature,
    int HpEvs,
    int AtkEvs,
    int DefEvs,
    int SpaEvs,
    int SpdEvs,
    int SpeEvs);
