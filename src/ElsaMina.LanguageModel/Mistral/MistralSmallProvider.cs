using ElsaMina.Core.Services.Http;
using ElsaMina.LanguageModel;

namespace ElsaMina.LanguageModel.Mistral;

public class MistralSmallProvider : MistralLanguageModelProvider
{
    public MistralSmallProvider(IHttpService httpService, ILanguageModelConfiguration configuration) : base(httpService,
        configuration)
    {
    }

    protected override string Model => "mistral-small-latest";
}
