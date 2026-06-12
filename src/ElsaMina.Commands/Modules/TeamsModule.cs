using Autofac;
using ElsaMina.Commands.Teams.Samples;
using ElsaMina.Commands.Teams.TeamPreviewOnLink;
using ElsaMina.Commands.Teams.TeamProviders;
using ElsaMina.Commands.Teams.TeamProviders.CoupCritique;
using ElsaMina.Commands.Teams.TeamProviders.Pokepaste;
using ElsaMina.Commands.Teams.TeamProviders.Showdown;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class TeamsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<AddTeamCommand>();
        builder.RegisterCommand<AddTeamToRoomCommand>();
        builder.RegisterCommand<TeamShowcaseCommand>();
        builder.RegisterCommand<TeamListCommand>();
        builder.RegisterCommand<DeleteTeamCommand>();
        builder.RegisterCommand<DeleteAllTeamsByTierCommand>();

        builder.RegisterHandler<DisplayTeamOnLinkHandler>();

        builder.RegisterType<PokepasteProvider>().As<ITeamProvider>().SingleInstance();
        builder.RegisterType<CoupCritiqueProvider>().As<ITeamProvider>().SingleInstance();
        builder.RegisterType<ShowdownTeamProvider>().As<ITeamProvider>().SingleInstance();
        builder.RegisterType<TeamLinkMatchFactory>().As<ITeamLinkMatchFactory>().SingleInstance();
    }
}
