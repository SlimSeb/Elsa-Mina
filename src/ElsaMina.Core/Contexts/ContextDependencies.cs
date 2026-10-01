using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;

namespace ElsaMina.Core.Contexts;

/// <summary>
/// The injected collaborators every context shares, grouped so that context
/// constructors only take the data specific to the received message.
/// </summary>
public sealed record ContextDependencies(
    IConfiguration Configuration,
    IResourcesService ResourcesService,
    IRoomsManager RoomsManager,
    IUserDetailsManager UserDetailsManager,
    IBot Bot);
