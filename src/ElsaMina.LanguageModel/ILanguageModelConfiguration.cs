namespace ElsaMina.LanguageModel;

/// <summary>
/// API keys of the language model providers. A provider without a key is skipped.
/// </summary>
public interface ILanguageModelConfiguration
{
    string MistralApiKey { get; }
    string ChatGptApiKey { get; }
    string GeminiApiKey { get; }
}
