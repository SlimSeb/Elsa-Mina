using ElsaMina.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Thin wrapper over an ONNX Runtime session. InferenceSession.Run is thread-safe, so a
/// single instance serves every battle room concurrently. The ~1M-parameter model runs
/// in well under a millisecond on CPU.
/// </summary>
public sealed class MiniPokeOnnxModel : IMiniPokeModel, IDisposable
{
    private readonly InferenceSession _session;

    public MiniPokeOnnxModel(MiniPokeModelOptions options)
    {
        if (!File.Exists(options.ModelPath))
        {
            Log.Warning("Battle model not found at {Path}; {Strategy} disabled", options.ModelPath, options.Strategy);
            return;
        }

        using var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        // Tiny model: one thread per call beats thread-pool overhead
        sessionOptions.IntraOpNumThreads = 1;
        _session = new InferenceSession(options.ModelPath, sessionOptions);
        Log.Information("{Strategy} battle model loaded from {Path}", options.Strategy, options.ModelPath);
    }

    public bool IsLoaded => _session != null;

    public (float[] Logits, float Value) Evaluate(BattleObservation observation)
    {
        const int mons = BattleObservation.MonCount;
        const int moves = BattleObservation.MoveCount;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("mon_ids", new DenseTensor<long>(observation.MonIds, [1, mons, 3])),
            NamedOnnxValue.CreateFromTensor("move_ids", new DenseTensor<long>(observation.MoveIds, [1, mons, moves])),
            NamedOnnxValue.CreateFromTensor("mon_feats",
                new DenseTensor<float>(observation.MonFeatures, [1, mons, observation.MonFeatureSize])),
            NamedOnnxValue.CreateFromTensor("move_feats",
                new DenseTensor<float>(observation.MoveFeatures, [1, mons, moves, observation.MoveFeatureSize])),
            NamedOnnxValue.CreateFromTensor("field",
                new DenseTensor<float>(observation.Field, [1, observation.Field.Length])),
            NamedOnnxValue.CreateFromTensor("mask",
                new DenseTensor<bool>(observation.Mask, [1, BattleObservation.ActionCount]))
        };

        using var results = _session.Run(inputs);
        var logits = results.First(result => result.Name == "logits").AsEnumerable<float>().ToArray();
        var value = results.First(result => result.Name == "value").AsEnumerable<float>().First();
        return (logits, value);
    }

    public void Dispose() => _session?.Dispose();
}
