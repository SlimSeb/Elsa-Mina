using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;

namespace ElsaMina.Core.Contexts;

/// <summary>
/// Les collaborateurs injectés que tous les contextes partagent, regroupés pour que les
/// constructeurs de contexte ne prennent que les données propres au message reçu.
/// </summary>
public sealed record ContextDependencies(
    IConfiguration Configuration,
    IResourcesService ResourcesService,
    IRoomsManager RoomsManager,
    IUserDetailsManager UserDetailsManager,
    IBot Bot);
