using ElsaMina.Core.Contexts;

namespace ElsaMina.Commands.RoomDashboard;

public interface IRoomDashboardService
{
    Task SendDashboardPageAsync(IContext context, string roomId, CancellationToken cancellationToken = default);
    Task SendOptionsPageAsync(IContext context, string roomId, CancellationToken cancellationToken = default);
}
