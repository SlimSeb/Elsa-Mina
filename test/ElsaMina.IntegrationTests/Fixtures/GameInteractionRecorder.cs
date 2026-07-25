using System.Globalization;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.IntegrationTests.Fixtures;

/// <summary>
/// Records, in order, every outward interaction a card game performs: the updatable chat panels it
/// posts, the HTML pages it pushes to players, the localized replies and PMs it sends, and the
/// templates it renders. The resulting trace is the characterization surface for the panel lifecycle.
/// </summary>
public sealed class GameInteractionRecorder
{
    private readonly List<string> _entries = [];
    private readonly List<(string Value, string Replacement)> _masks = [];

    public GameInteractionRecorder()
    {
        Context = new RecordingContext(this);
        TemplatesManager = new RecordingTemplatesManager(this);
    }

    /// <summary>
    /// The <see cref="IContext"/> to hand to the game under test.
    /// </summary>
    public IContext Context { get; }

    /// <summary>
    /// The <see cref="ITemplatesManager"/> to hand to the game under test. It renders every template
    /// as the empty string, so the trace stays independent of the .cshtml files.
    /// </summary>
    public ITemplatesManager TemplatesManager { get; }

    public IReadOnlyList<string> Entries => _entries;

    /// <summary>
    /// The recorded entries of a single kind, e.g. <c>"panel"</c> or <c>"reply"</c>.
    /// </summary>
    public IReadOnlyList<string> EntriesOfKind(string kind) =>
        _entries.Where(entry => entry.StartsWith(kind + " ", StringComparison.Ordinal)).ToList();

    /// <summary>
    /// The <c>SendUpdatableHtml</c> calls only, as <c>"panelId new|update|clear"</c>.
    /// </summary>
    public IReadOnlyList<string> PanelTrace() => EntriesOfKind("panel")
        .Select(entry => entry["panel ".Length..])
        .ToList();

    public void Clear() => _entries.Clear();

    /// <summary>
    /// Rewrites <paramref name="value"/> to <paramref name="replacement"/> in every recorded entry.
    /// Panel identifiers embed a process-wide game counter, so they have to be masked for a trace to be
    /// comparable against a golden file. Masks are applied in registration order, so register the
    /// longer identifier first when one is a prefix of another.
    /// </summary>
    public void AddMask(string value, string replacement) => _masks.Add((value, replacement));

    /// <summary>
    /// Masks the panel identifiers of a game whose panels are named <c>"{prefix}-{gameId}..."</c>.
    /// </summary>
    public void MaskGameId(string prefix, int gameId) => AddMask($"{prefix}-{gameId}", $"{prefix}-#");

    /// <summary>
    /// Longest block of consecutive entries the compressor will try to fold. A whole trick of a card
    /// game repeats identically, so this has to comfortably exceed one trick's worth of interactions.
    /// </summary>
    private const int MAX_BLOCK_LENGTH = 192;

    /// <summary>
    /// The trace with repeated consecutive blocks folded into <c>&lt;&lt;repeat N&gt;&gt;</c> sections.
    /// Lossless, and short enough that a whole deal fits in a reviewable golden file: every trick of a
    /// card game produces the same interactions, so the eighteen tricks of a tarot deal collapse to one.
    /// </summary>
    public IReadOnlyList<string> CompressedTrace()
    {
        var compressed = new List<string>();
        var index = 0;

        while (index < _entries.Count)
        {
            var (blockLength, repeats) = FindLongestRepeatAt(index);

            if (repeats == 1)
            {
                compressed.Add(_entries[index]);
            }
            else if (blockLength == 1)
            {
                compressed.Add($"{_entries[index]} x{repeats}");
            }
            else
            {
                compressed.Add($"<<repeat {repeats}>>");
                compressed.AddRange(_entries.Skip(index).Take(blockLength));
                compressed.Add("<<end>>");
            }

            index += blockLength * repeats;
        }

        return compressed;
    }

    /// <summary>
    /// The block starting at <paramref name="start"/> that covers the most entries by repeating itself
    /// back to back. Ties go to the shorter block, which compresses more aggressively.
    /// </summary>
    private (int BlockLength, int Repeats) FindLongestRepeatAt(int start)
    {
        var bestLength = 1;
        var bestRepeats = 1;
        var maxLength = Math.Min(MAX_BLOCK_LENGTH, _entries.Count - start);

        for (var length = 1; length <= maxLength; length++)
        {
            // A repeat must at least start with the same entry, which prunes most candidate lengths.
            if (start + length >= _entries.Count || _entries[start] != _entries[start + length])
            {
                continue;
            }

            var repeats = 1;
            while (start + (repeats + 1) * length <= _entries.Count && BlocksMatch(start, start + repeats * length, length))
            {
                repeats++;
            }

            if (repeats > 1 && length * repeats > bestLength * bestRepeats)
            {
                bestLength = length;
                bestRepeats = repeats;
            }
        }

        return (bestLength, bestRepeats);
    }

    private bool BlocksMatch(int firstStart, int secondStart, int length)
    {
        for (var offset = 0; offset < length; offset++)
        {
            if (_entries[firstStart + offset] != _entries[secondStart + offset])
            {
                return false;
            }
        }

        return true;
    }

    private void Record(string entry)
    {
        foreach (var (value, replacement) in _masks)
        {
            entry = entry.Replace(value, replacement, StringComparison.Ordinal);
        }

        _entries.Add(entry);
    }

    /// <summary>
    /// An <see cref="IContext"/> that records everything a card game sends outwards. Resource lookups
    /// echo the key back so the emitted keys stay assertable, mirroring the unit test convention.
    /// </summary>
    private sealed class RecordingContext : IContext
    {
        private readonly GameInteractionRecorder _recorder;

        public RecordingContext(GameInteractionRecorder recorder)
        {
            _recorder = recorder;
        }

        public string Message => string.Empty;
        public string RawMessage => string.Empty;
        public string Target => string.Empty;
        public IUser Sender => null;
        public IRoom Room => null;
        public string Command => string.Empty;
        public bool IsSenderWhitelisted => false;
        public string RoomId => "testroom";
        public bool IsPrivateMessage => false;
        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
        public ContextType Type => ContextType.Room;

        public string GetString(string key) => key;

        public string GetString(string key, params object[] formatArguments) => key;

        public void ReplyHtmlPage(string pageName, string html)
        {
        }

        public void SendHtmlPageTo(string userId, string pageName, string html) =>
            _recorder.Record($"page {userId} {pageName}");

        public void CloseHtmlPage(string userId, string pageName) =>
            _recorder.Record($"close {userId} {pageName}");

        public bool HasRankOrHigher(Rank requiredRank) => true;

        public Task<Rank> GetUserRankInRoom(string roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rank.Regular);

        public Task<bool> HasSufficientRankInRoom(string roomId, Rank requiredRank,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public void Reply(string message, bool rankAware = false) => _recorder.Record($"say {message}");

        public void SendMessageIn(string roomId, string message) => _recorder.Record($"say {message}");

        public void ReplyLocalizedMessage(string key, params object[] formatArguments) =>
            _recorder.Record($"reply {key}");

        public void ReplyRankAwareLocalizedMessage(string key, params object[] formatArguments) =>
            _recorder.Record($"reply {key}");

        public void ReplyHtml(string html, string roomId = null, bool rankAware = false)
        {
        }

        public void SendHtmlTo(string userId, string html, string roomId = null)
        {
        }

        public void SendUpdatableHtml(string htmlId, string html, bool isChanging) =>
            _recorder.Record($"panel {htmlId} {DescribePanelCall(html, isChanging)}");

        public void SendPrivateUpdatableHtml(string userId, string roomId, string htmlId, string html,
            bool isChanging) =>
            _recorder.Record($"privatepanel {userId} {htmlId} {DescribePanelCall(html, isChanging)}");

        public Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        /// <summary>
        /// Classifies a panel write: an empty body wipes the panel, otherwise it either creates it
        /// (<c>isChanging: false</c>, which posts a fresh panel at the bottom of the chat) or updates
        /// the existing one in place.
        /// </summary>
        private static string DescribePanelCall(string html, bool isChanging)
        {
            if (string.IsNullOrEmpty(html))
            {
                return isChanging ? "clear" : "clear-new";
            }

            return isChanging ? "update" : "new";
        }
    }

    /// <summary>
    /// An <see cref="ITemplatesManager"/> that records which template was asked for and renders a
    /// placeholder naming it. The body has to be non-empty: games signal "wipe this panel" by sending
    /// an empty body, so rendering nothing would make every panel write look like a clear.
    /// </summary>
    private sealed class RecordingTemplatesManager : ITemplatesManager
    {
        private readonly GameInteractionRecorder _recorder;

        public RecordingTemplatesManager(GameInteractionRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task CompileTemplatesAsync() => Task.CompletedTask;

        public Task<string> GetTemplateAsync(string templateKey, object model)
        {
            _recorder.Record($"tpl {templateKey}");
            return Task.FromResult($"[{templateKey}]");
        }
    }
}
