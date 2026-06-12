using Autofac;
using ElsaMina.Commands.Badges;
using ElsaMina.Commands.Badges.BadgeDisplay;
using ElsaMina.Commands.Badges.BadgeEditPanel;
using ElsaMina.Commands.Badges.BadgeHolders;
using ElsaMina.Commands.Badges.BadgeList;
using ElsaMina.Commands.Badges.HallOfFame;
using ElsaMina.Commands.Badges.UserBadgePanel;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Modules;

public class BadgesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterCommand<AddBadgeCommand>();
        builder.RegisterCommand<BadgeEditPanelCommand>();
        builder.RegisterCommand<UserBadgePanelCommand>();
        builder.RegisterCommand<EditBadgeCommand>();
        builder.RegisterCommand<GiveBadgeCommand>();
        builder.RegisterCommand<DeleteBadgeCommand>();
        builder.RegisterCommand<TakeBadgeCommand>();
        builder.RegisterCommand<BadgeDisplayCommand>();
        builder.RegisterCommand<BadgeHoldersCommand>();
        builder.RegisterCommand<BadgeListCommand>();
        builder.RegisterCommand<HallOfFameCommand>();
    }
}
