using ElsaMina.Core.Services.Http;
using ElsaMina.LanguageModel;

namespace ElsaMina.LanguageModel.Google;

public class GeminiFlashProvider : GeminiLanguageModelProvider
{
    public GeminiFlashProvider(ILanguageModelConfiguration configuration, IHttpService httpService) : base(configuration, httpService)
    {
    }

    protected override string Model => "gemini-3.6-flash";
}
