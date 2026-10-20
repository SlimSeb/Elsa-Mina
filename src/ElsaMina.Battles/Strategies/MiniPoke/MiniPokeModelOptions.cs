namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// One exported minipoke model version: export_onnx.py writes minipoke.onnx and encoder_spec.json into
/// <paramref name="ModelDirectory"/>.
/// </summary>
/// <param name="Strategy">The battle strategy that plays with this model.</param>
/// <param name="ModelDirectory">The directory holding minipoke.onnx and encoder_spec.json.</param>
/// <param name="Sample">Sample the action from the policy instead of taking the most likely one.</param>
/// <param name="Temperature">Softmax temperature when sampling.</param>
public sealed record MiniPokeModelOptions(
    BattleDecisionStrategy Strategy,
    string ModelDirectory,
    bool Sample = false,
    float Temperature = 1.0f)
{
    public string ModelPath => Path.Join(ModelDirectory, "minipoke.onnx");
    public string EncoderSpecPath => Path.Join(ModelDirectory, "encoder_spec.json");
}
