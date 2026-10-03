using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.LanguageModel;

namespace ElsaMina.Core.Services.LanguageModel.OpenAi;

public class GptMiniProvider : GptLanguageModelProvider
{
    public GptMiniProvider(IHttpService httpService, IConfiguration configuration) : base(httpService, configuration)
    {
    }

    protected override string Model => "gpt-5.4-mini";
}
