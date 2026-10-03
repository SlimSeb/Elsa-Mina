using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.LanguageModel;

namespace ElsaMina.Core.Services.LanguageModel.Mistral;

public class MistralSmallProvider : MistralLanguageModelProvider
{
    public MistralSmallProvider(IHttpService httpService, IConfiguration configuration) : base(httpService,
        configuration)
    {
    }

    protected override string Model => "mistral-small-latest";
}
