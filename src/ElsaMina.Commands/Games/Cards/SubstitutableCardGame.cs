using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.Cards;

/// <summary>
/// A seated card game that shows each player their hand on a private HTML page, narrates itself into a
/// running log panel, and lets seats change hands mid-game. Tarot, belote and président all work this
/// way; poker does not, because its seats hold real bucks and its hands live in private chat panels.
/// </summary>
/// <typeparam name="TPlayer">The game's own seat type.</typeparam>
public abstract class SubstitutableCardGame<TPlayer> : SeatedCardGame<TPlayer>, ISubstitutableCardGame
    where TPlayer : class, ISubstitutablePlayer
{
    private readonly List<string> _log = [];

    private bool _subPanelInitialized;
    private bool _logPanelInitialized;
    private int _renderedLogCount;

    protected SubstitutableCardGame(ITemplatesManager templatesManager, TimeSpan turnTimeout,
        TimeSpan? turnWarningRemaining) : base(templatesManager, turnTimeout, turnWarningRemaining)
    {
    }

    /// <summary>
    /// The narration of the game so far, one localized line per event.
    /// </summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>
    /// Puts the game into its finished state. Called when the game is called off, so the shared
    /// teardown can run without knowing the game's phase enum.
    /// </summary>
    protected abstract void MarkFinished();

    #region Substitutions

    public Task<(bool Success, string MessageKey, object[] Args)> RequestSubAsync(IUser user) =>
        RunSubActionAsync(async () =>
        {
            if (IsInLobby || IsFinished)
            {
                return (false, Key("sub_not_active"), []);
            }

            var player = FindSeat(user.UserId);
            if (player is null)
            {
                return (false, Key("sub_not_a_player"), []);
            }

            return await MarkSeatForSubstitutionAsync(player, forced: false);
        });

    /// <summary>
    /// Lets staff put a player up for substitution on their behalf, marking that seat as looking for a
    /// replacement without the player having to ask themselves.
    /// </summary>
    public Task<(bool Success, string MessageKey, object[] Args)> ForceRequestSubAsync(string targetPlayerId) =>
        RunSubActionAsync(async () =>
        {
            if (IsInLobby || IsFinished)
            {
                return (false, Key("sub_not_active"), []);
            }

            if (string.IsNullOrWhiteSpace(targetPlayerId))
            {
                return (false, Key("sub_force_no_target"), []);
            }

            var player = FindSeat(targetPlayerId.ToLowerAlphaNum());
            if (player is null)
            {
                return (false, Key("sub_force_not_a_player"), []);
            }

            if (player.WantsSub)
            {
                return (false, Key("sub_force_already"), [player.Name]);
            }

            return await MarkSeatForSubstitutionAsync(player, forced: true);
        });

    public Task<(bool Success, string MessageKey, object[] Args)> AcceptSubAsync(IUser user,
        string targetPlayerId) =>
        RunSubActionAsync(async () =>
        {
            if (IsInLobby || IsFinished)
            {
                return (false, Key("sub_not_active"), []);
            }

            if (HasPlayer(user.UserId))
            {
                return (false, Key("sub_already_player"), []);
            }

            var pending = Seats.Where(player => player.WantsSub).ToList();
            if (pending.Count == 0)
            {
                return (false, Key("sub_none_pending"), []);
            }

            var target = string.IsNullOrWhiteSpace(targetPlayerId)
                ? pending[0]
                : pending.FirstOrDefault(player => player.UserId == targetPlayerId.ToLowerAlphaNum());
            if (target is null)
            {
                return (false, Key("sub_invalid_target"), []);
            }

            var leavingUserId = target.UserId;
            var leavingName = target.Name;
            Context.CloseHtmlPage(leavingUserId, PlayerPageId);
            target.SubstituteWith(user);

            Context.ReplyLocalizedMessage(Key("sub_done"), user.Name, leavingName);
            await RenderAllAsync();
            await RenderSubPanelAsync();
            return (true, null, []);
        });

    /// <summary>
    /// Flags a seat as looking for a replacement, or clears the flag when the player asks a second
    /// time. A fresh request re-posts the sub panel so it drops to the bottom of the chat instead of
    /// staying stuck high up in the scrollback.
    /// </summary>
    private async Task<(bool Success, string MessageKey, object[] Args)> MarkSeatForSubstitutionAsync(
        TPlayer player, bool forced)
    {
        // A second request from the same player cancels their pending sub.
        if (!forced && player.WantsSub)
        {
            player.WantsSub = false;
            Context.ReplyLocalizedMessage(Key("sub_cancelled"), player.Name);
            await RenderSubPanelAsync();
            return (true, null, []);
        }

        player.WantsSub = true;
        Context.ReplyLocalizedMessage(Key(forced ? "sub_force_requested" : "sub_requested"), player.Name);
        await RenderSubPanelAsync(forceResend: true);
        return (true, null, []);
    }

    private async Task<(bool Success, string MessageKey, object[] Args)> RunSubActionAsync(
        Func<Task<(bool Success, string MessageKey, object[] Args)>> action)
    {
        (bool Success, string MessageKey, object[] Args) result = default;
        await RunActionAsync(async () => result = await action());
        return result;
    }

    protected async Task RenderSubPanelAsync(bool forceResend = false)
    {
        if (Seats.All(player => !player.WantsSub))
        {
            ClearSubPanel();
            return;
        }

        if (forceResend)
        {
            ClearSubPanel();
        }

        var html = await TemplatesManager.GetTemplateAsync(TemplateKey("Sub"), BuildModel(null));
        Context.SendUpdatableHtml(SubPanelId, html.RemoveNewlines(), isChanging: _subPanelInitialized);
        _subPanelInitialized = true;
    }

    protected void ClearSubPanel()
    {
        if (!_subPanelInitialized)
        {
            return;
        }

        Context.SendUpdatableHtml(SubPanelId, string.Empty, isChanging: true);
        _subPanelInitialized = false;
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Appends a localized game event to the running log shown (collapsed) on the public panel and the
    /// player pages. Replaces chat broadcasts so the narration lives inside the game HTML.
    /// </summary>
    protected void LogEvent(string key, params object[] args)
    {
        _log.Add(Context.GetString(key, args));
    }

    protected async Task RenderAllAsync(bool resendPublic = false, bool resendLog = false)
    {
        await RenderPublicAsync(resendPublic);
        await RenderPublicLogAsync(resendLog);
        await RenderPlayerPagesAsync();
    }

    /// <summary>
    /// Renders the running game log into its own updatable chat panel, kept separate from the main table
    /// panel so it only re-renders when a new event has actually been logged. When <paramref name="forceResend"/>
    /// is set (a fresh deal), it is re-posted at the bottom of the chat instead of updated in place.
    /// </summary>
    private async Task RenderPublicLogAsync(bool forceResend = false)
    {
        // The final result panel embeds the whole log itself, so drop the live panel once the game ends.
        if (IsFinished)
        {
            ClearLogPanel();
            return;
        }

        if (_log.Count == 0 || (!forceResend && _logPanelInitialized && _renderedLogCount == _log.Count))
        {
            return;
        }

        var html = await TemplatesManager.GetTemplateAsync(TemplateKey("Log"), BuildModel(null));
        Context.SendUpdatableHtml(LogPanelId, html.RemoveNewlines(),
            isChanging: _logPanelInitialized && !forceResend);
        _logPanelInitialized = true;
        _renderedLogCount = _log.Count;
    }

    protected void ClearLogPanel()
    {
        if (!_logPanelInitialized)
        {
            return;
        }

        Context.SendUpdatableHtml(LogPanelId, string.Empty, isChanging: true);
        _logPanelInitialized = false;
        _renderedLogCount = 0;
    }

    protected async Task RenderPlayerPagesAsync()
    {
        foreach (var player in Seats)
        {
            await RenderPlayerPageAsync(player);
        }
    }

    /// <summary>
    /// Renders a player's private HTML page: the public table (or result) on top and the player's own
    /// hand with action buttons below. HTML pages update in place, so no chat re-posting is needed.
    /// </summary>
    protected async Task RenderPlayerPageAsync(TPlayer player)
    {
        var model = BuildModel(player);

        if (IsFinished)
        {
            var resultHtml = await TemplatesManager.GetTemplateAsync(TemplateKey("Result"), model);
            Context.SendHtmlPageTo(player.UserId, PlayerPageId, resultHtml.RemoveNewlines());
            return;
        }

        var tableHtml = await TemplatesManager.GetTemplateAsync(TemplateKey("Table"), model);
        var handHtml = await TemplatesManager.GetTemplateAsync(TemplateKey("Hand"), model);
        var logHtml = _log.Count > 0
            ? await TemplatesManager.GetTemplateAsync(TemplateKey("Log"), model)
            : string.Empty;
        Context.SendHtmlPageTo(player.UserId, PlayerPageId,
            tableHtml.RemoveNewlines() + logHtml.RemoveNewlines() + handHtml.RemoveNewlines());
    }

    protected void ClosePlayerPages()
    {
        foreach (var player in Seats)
        {
            Context.CloseHtmlPage(player.UserId, PlayerPageId);
        }
    }

    public Task ResendPlayerPageAsync(IUser user) => RunActionAsync(async () =>
    {
        if (IsInLobby)
        {
            return;
        }

        var player = FindSeat(user.UserId);
        if (player is null)
        {
            return;
        }

        await RenderPlayerPageAsync(player);
    });

    #endregion

    public override async Task CancelAsync()
    {
        StopTurnTimer();

        // Cancelled while still gathering players: replace the lobby panel (with its join/start buttons)
        // by a clear "cancelled" notice so the public panel does not look like it is still open.
        if (IsInLobby)
        {
            await RenderCancelledPublicAsync();
        }
        else
        {
            // A cancelled deal has no result to show, so close the hand pages rather than leave every
            // player looking at a stale hand of a game that is over.
            ClosePlayerPages();
        }

        MarkFinished();
        ClearSubPanel();
        ClearLogPanel();
        OnEnd();
    }
}
