using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;

namespace ElsaMina.Core.Services.LanguageModel.OpenAi;

public class Gpt4OMiniProvider : GptLanguageModelProvider
{
    public Gpt4OMiniProvider(IHttpService httpService, IConfiguration configuration) : base(httpService, configuration)
    {
    }

    protected override string Model => "gpt-4o-mini";
}
