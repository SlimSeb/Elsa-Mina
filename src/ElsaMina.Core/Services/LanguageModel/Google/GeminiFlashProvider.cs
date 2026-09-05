using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Core.Services.LanguageModel.Google;

public class GeminiFlashProvider : GeminiLanguageModelProvider
{
    public GeminiFlashProvider(IConfiguration configuration, IHttpService httpService) : base(configuration, httpService)
    {
    }

    protected override string Model => "gemini-flash-latest";
}
