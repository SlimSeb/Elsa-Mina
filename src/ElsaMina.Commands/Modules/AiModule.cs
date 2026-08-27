using Autofac;
using ElsaMina.Commands.Ai.Calc;
using ElsaMina.Commands.Ai.Chat;
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
        builder.RegisterType<ConversationHistoryService>().As<IConversationHistoryService>().SingleInstance();
        builder.RegisterType<PersonalityService>().As<IPersonalityService>().SingleInstance();
        builder.RegisterType<DamageCalculator>().As<IDamageCalculator>().SingleInstance();
    }
}
