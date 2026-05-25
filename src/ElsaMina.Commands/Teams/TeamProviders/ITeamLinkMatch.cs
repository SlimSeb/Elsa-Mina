namespace ElsaMina.Commands.Teams.TeamProviders;

public interface ITeamLinkMatch
{
    Task<SharedTeam> GetTeamExport(CancellationToken cancellationToken = default);
}