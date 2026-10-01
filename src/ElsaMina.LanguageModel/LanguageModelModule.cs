using Autofac;
using ElsaMina.LanguageModel.Google;
using ElsaMina.LanguageModel.Mistral;
using ElsaMina.LanguageModel.OpenAi;

namespace ElsaMina.LanguageModel;

public class LanguageModelModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<GeminiFlashProvider>().AsSelf().SingleInstance();
        builder.RegisterType<MistralSmallProvider>().AsSelf().SingleInstance();
        builder.RegisterType<GptMiniProvider>().AsSelf().SingleInstance();
        builder.RegisterType<LanguageModelResolver>().As<ILanguageModelProvider>().SingleInstance();
    }
}
