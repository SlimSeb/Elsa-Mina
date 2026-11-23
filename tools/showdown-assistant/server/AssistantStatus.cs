namespace ElsaMina.BattleAssistant;

/// <summary>
/// The server's health, for the extension's options page and panel.
/// </summary>
/// <param name="Model">The model's strategy name ("gladionv1-22m").</param>
/// <param name="Loaded">Whether its ONNX file and encoder spec were found.</param>
/// <param name="CanSearch">Whether Showdown's simulator is configured for the one-turn search.</param>
/// <param name="TrainedFormat">The format the model plays well.</param>
public sealed record AssistantStatus(string Model, bool Loaded, bool CanSearch, string TrainedFormat);
