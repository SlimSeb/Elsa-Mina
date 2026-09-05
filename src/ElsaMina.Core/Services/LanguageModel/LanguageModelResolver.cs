using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.LanguageModel.Google;
using ElsaMina.Core.Services.LanguageModel.Mistral;
using ElsaMina.Core.Services.LanguageModel.OpenAi;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.LanguageModel;

/// <summary>
/// Attempts multiple language model providers in a fixed order until one succeeds.
/// </summary>
public class LanguageModelResolver : ILanguageModelProvider
{
    private readonly IConfiguration _configuration;
    private readonly IDependencyContainerService _dependencyContainer;
    private List<ILanguageModelProvider> _cachedProviders;

    public LanguageModelResolver(IConfiguration configuration, IDependencyContainerService dependencyContainer)
    {
        _configuration = configuration;
        _dependencyContainer = dependencyContainer;
    }

    /// <summary>
    /// Sends a single prompt and falls back to other providers if the call fails.
    /// </summary>
    public Task<string> AskLanguageModelAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackAsync(provider =>
        {
            Log.Information("Sending prompt to language model provider {Prompt}", prompt);
            return provider.AskLanguageModelAsync(prompt, cancellationToken);
        });
    }

    /// <summary>
    /// Sends a full conversation request and falls back to other providers if the call fails.
    /// </summary>
    public Task<string> AskLanguageModelAsync(LanguageModelRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackAsync(provider =>
        {
            Log.Information("Sending request to language model provider {Request}", request);
            return provider.AskLanguageModelAsync(request, cancellationToken);
        });
    }

    /// <summary>
    /// Tries each resolved provider in order, returning the first non-empty response.
    /// </summary>
    private async Task<string> ExecuteWithFallbackAsync(Func<ILanguageModelProvider, Task<string>> action)
    {
        var providers = _cachedProviders ??= GetProviders();

        if (providers.Count == 0)
        {
            Log.Error("No language model providers are configured with API keys.");
            return null;
        }

        foreach (var provider in providers)
        {
            try
            {
                var response = await action(provider);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    return response;
                }

                Log.Warning("Language model provider {0} returned an empty response.", provider.GetType().Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Language model provider {0} failed.", provider.GetType().Name);
            }
        }

        return null;
    }

    private List<ILanguageModelProvider> GetProviders()
    {
        var providers = new List<ILanguageModelProvider>();

        // In order of priority
        if (!string.IsNullOrWhiteSpace(_configuration.GeminiApiKey))
        {
            providers.Add(_dependencyContainer.Resolve<GeminiFlashProvider>());
        }

        if (!string.IsNullOrWhiteSpace(_configuration.MistralApiKey))
        {
            providers.Add(_dependencyContainer.Resolve<MistralSmallProvider>());
        }

        if (!string.IsNullOrWhiteSpace(_configuration.ChatGptApiKey))
        {
            providers.Add(_dependencyContainer.Resolve<GptMiniProvider>());
        }

        return providers;
    }
}