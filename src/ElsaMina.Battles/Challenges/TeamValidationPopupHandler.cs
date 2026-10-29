using ElsaMina.Core.Handlers;

namespace ElsaMina.Battles.Challenges;

/// <summary>
/// Passes the server's popups to <see cref="ITeamValidator"/>, which waits for /vtm's answer.
/// </summary>
public class TeamValidationPopupHandler : Handler
{
    private readonly ITeamValidator _teamValidator;

    public TeamValidationPopupHandler(ITeamValidator teamValidator)
    {
        _teamValidator = teamValidator;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "popup" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length > 2)
        {
            _teamValidator.OnPopup(string.Join("|", parts[2..]));
        }

        return Task.CompletedTask;
    }
}
