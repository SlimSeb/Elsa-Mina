using Autofac;
using ElsaMina.Commands.Ai.Calc;
using ElsaMina.Commands.Ai.Chat;
using ElsaMina.Commands.Ai.LanguageModel;
using ElsaMina.Commands.Ai.LanguageModel.Google;
using ElsaMina.Commands.Ai.LanguageModel.Mistral;
using ElsaMina.Commands.Ai.LanguageModel.OpenAi;
using ElsaMina.Commands.Ai.TextToSpeech;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class AiModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<AskElsaCommand>();
        builder.RegisterCommand<SetPersonalityCommand>();
        builder.RegisterCommand<CalcWithAiCommand>();
        builder.RegisterCommand<SpeakCommand>();

        builder.RegisterType<ElevenLabsAiTextToSpeechProvider>().As<IAiTextToSpeechProvider>().SingleInstance();
        builder.RegisterType<Gemini25FlashProvider>().AsSelf().SingleInstance();
        builder.RegisterType<MistralMediumProvider>().AsSelf().SingleInstance();
        builder.RegisterType<GptNano41Provider>().AsSelf().SingleInstance();
        builder.RegisterType<ConversationHistoryService>().As<IConversationHistoryService>().SingleInstance();
        builder.RegisterType<PersonalityService>().As<IPersonalityService>().SingleInstance();
        builder.RegisterType<LanguageModelResolver>().As<ILanguageModelProvider>().SingleInstance();
        builder.RegisterType<DamageCalculator>().As<IDamageCalculator>().SingleInstance();
    }
}
