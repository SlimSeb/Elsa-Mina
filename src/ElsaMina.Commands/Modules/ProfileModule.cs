using Autofac;
using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class ProfileModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<ProfileCommand>();
        builder.RegisterCommand<SetEmojiCommand>();
        builder.RegisterCommand<SetProfileColorCommand>();
        builder.RegisterCommand<EditProfilePanelCommand>();
        builder.RegisterCommand<SetAvatarCommand>();
        builder.RegisterCommand<SetTitleCommand>();

        builder.RegisterType<ProfileService>().As<IProfileService>().SingleInstance();
    }
}
