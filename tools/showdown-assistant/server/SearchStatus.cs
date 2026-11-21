namespace ElsaMina.BattleAssistant;

public enum SearchStatus
{
    /// <summary>The search chose <see cref="SearchAdvice.Action"/>.</summary>
    Searched,

    /// <summary>Nothing to search: a forced switch, or a single candidate.</summary>
    Skipped,

    /// <summary>The request was answered or replaced since the advice.</summary>
    Stale,

    /// <summary>No simulator configured.</summary>
    Unavailable,

    /// <summary>The search failed or ran out of time: see <see cref="SearchAdvice.Message"/>.</summary>
    Failed
}
