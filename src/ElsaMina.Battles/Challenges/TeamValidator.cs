namespace ElsaMina.Battles.Challenges;

/// <summary>
/// /utm the team then /vtm the format: the server answers with a popup, "Your team is valid for [Gen 9] OU." or "Your
/// team was rejected for the following reasons: ...". One validation at a time: popups carry no request id.
/// </summary>
public sealed class TeamValidator : ITeamValidator, IDisposable
{
    private const string VALID = "Your team is valid for";
    private const string REJECTED = "Your team was rejected";
    // Monitor.countPrepBattle and /vtm's answer to an unknown format
    private const string RATE_LIMITED = "you are limited to";
    private const string UNKNOWN_FORMAT = "Please provide a valid format";
    private static readonly TimeSpan ANSWER_TIMEOUT = TimeSpan.FromSeconds(10);

    private readonly IBattleTeamSender _teamSender;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private TaskCompletionSource<bool?> _pending;

    public TeamValidator(IBattleTeamSender teamSender)
    {
        _teamSender = teamSender;
    }

    public async Task<bool?> ValidateAsync(string packedTeam, string format,
        CancellationToken cancellationToken = default)
    {
        await _oneAtATime.WaitAsync(cancellationToken);
        try
        {
            var answer = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _pending, answer);
            _teamSender.SendWithTeam(packedTeam, $"/vtm {format}");
            try
            {
                return await answer.Task.WaitAsync(ANSWER_TIMEOUT, cancellationToken);
            }
            catch (TimeoutException)
            {
                return null;
            }
        }
        finally
        {
            Volatile.Write(ref _pending, null);
            _oneAtATime.Release();
        }
    }

    public void OnPopup(string popup)
    {
        var pending = Volatile.Read(ref _pending);
        if (pending == null || popup == null)
        {
            return;
        }

        if (popup.Contains(VALID, StringComparison.Ordinal))
        {
            pending.TrySetResult(true);
        }
        else if (popup.Contains(REJECTED, StringComparison.Ordinal))
        {
            pending.TrySetResult(false);
        }
        else if (popup.Contains(RATE_LIMITED, StringComparison.Ordinal) ||
                 popup.Contains(UNKNOWN_FORMAT, StringComparison.Ordinal))
        {
            pending.TrySetResult(null);
        }
    }

    public void Dispose() => _oneAtATime.Dispose();
}
