using System.Text.Json;
using ElsaMina.Battles.Dtos;
using ElsaMina.Core.Handlers;
using ElsaMina.Logging;

namespace ElsaMina.Battles;

/// <summary>
/// Showdown sends |updatesearch| on login and whenever a search or a game of the bot starts or ends.
/// </summary>
public class UpdateSearchHandler : Handler
{
    private readonly ILadderingService _ladderingService;

    public UpdateSearchHandler(ILadderingService ladderingService)
    {
        _ladderingService = ladderingService;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "updatesearch" };

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 3)
        {
            return Task.CompletedTask;
        }

        var json = string.Join("|", parts[2..]);
        SearchStateDto searchState;
        try
        {
            searchState = JsonSerializer.Deserialize<SearchStateDto>(json);
        }
        catch (JsonException exception)
        {
            Log.Error(exception, "Could not read search state {SearchState}", json);
            return Task.CompletedTask;
        }

        if (searchState != null)
        {
            _ladderingService.OnSearchStateUpdated(searchState.Searching is { Count: > 0 },
                searchState.Games is { Count: > 0 });
        }

        return Task.CompletedTask;
    }
}
