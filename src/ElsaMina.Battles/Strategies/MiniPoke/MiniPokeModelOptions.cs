namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// One exported minipoke model version: export_onnx.py writes minipoke.onnx and encoder_spec.json into
/// <paramref name="ModelDirectory"/>.
/// </summary>
/// <param name="Strategy">The battle strategy that plays with this model.</param>
/// <param name="ModelDirectory">The directory holding minipoke.onnx and encoder_spec.json.</param>
/// <param name="Search">Whether the strategy searches one turn ahead (a model's search variant).</param>
public sealed record MiniPokeModelOptions(BattleDecisionStrategy Strategy, string ModelDirectory, bool Search = false)
{
    public string ModelPath => Path.Join(ModelDirectory, "minipoke.onnx");
    public string EncoderSpecPath => Path.Join(ModelDirectory, "encoder_spec.json");
    public string ValueCalibrationPath => Path.Join(ModelDirectory, "value_calibration.json");
}
