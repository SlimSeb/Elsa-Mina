using ElsaMina.Core.Services.Http;
using ElsaMina.LanguageModel;

namespace ElsaMina.LanguageModel.OpenAi;

public class GptMiniProvider : GptLanguageModelProvider
{
    public GptMiniProvider(IHttpService httpService, ILanguageModelConfiguration configuration) : base(httpService, configuration)
    {
    }

    protected override string Model => "gpt-5.4-mini";
}
