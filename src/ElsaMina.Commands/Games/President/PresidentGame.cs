using ElsaMina.Commands.Games.Cards;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.President;

public class PresidentGame : SubstitutableCardGame<PresidentPlayer>, IPresidentGame
{
    private readonly IRandomService _randomService;
    private readonly IConfiguration _configuration;

    private readonly List<PresidentPlayer> _finishOrder = [];
    private readonly List<PresidentPlayer> _twoFinishers = [];

    private bool _matchRequired;

    [UsedImplicitly]
    public PresidentGame(IRandomService randomService, ITemplatesManager templatesManager,
        IConfiguration configuration)
        : this(randomService, templatesManager, configuration, PresidentConstants.TURN_TIMEOUT)
    {
    }

    public PresidentGame(IRandomService randomService, ITemplatesManager templatesManager,
        IConfiguration configuration, TimeSpan turnTimeout)
        : base(templatesManager, turnTimeout, PresidentConstants.TURN_TIMEOUT_WARNING_REMAINING)
    {
        _randomService = randomService;
        _configuration = configuration;
    }

    public override string Identifier => nameof(PresidentGame);

    public PresidentPhase Phase { get; private set; } = PresidentPhase.Lobby;

    public override bool IsInLobby => Phase == PresidentPhase.Lobby;

    protected override string ResourcePrefix => "president";
    protected override string TemplateFolder => "President";
    protected override int MinPlayers => PresidentConstants.MIN_PLAYERS;
    protected override int MaxPlayers => PresidentConstants.MAX_PLAYERS;
    protected override bool IsFinished => Phase == PresidentPhase.Finished;
    protected override bool IsAcceptingActions => Phase is PresidentPhase.Exchange or PresidentPhase.Playing;

    protected override PresidentPlayer CreatePlayer(IUser user) => new(user);

    protected override void MarkFinished() => Phase = PresidentPhase.Finished;

    public Task<(bool Success, string MessageKey, object[] Args)> LeaveAsync(IUser user) => LeaveSeatAsync(user);

    public PresidentPlayer CurrentPlayer => Phase == PresidentPhase.Playing ? CurrentSeat : null;

    public int RoundNumber { get; private set; }
    public int TotalRounds { get; set; } = PresidentConstants.DEFAULT_ROUNDS;

    public PresidentTrick CurrentTrick { get; private set; } = new();

    /// <summary>
    /// True while the "ou rien" rule pins the current player: the last play matched the rank of the
    /// one before it, so they must play that exact rank or sit the turn out (without leaving the trick).
    /// </summary>
    public bool IsMatchRequired => _matchRequired;

    public PresidentPlayer LastTrickWinner { get; private set; }
    public IReadOnlyList<PresidentPlayer> FinishOrder => _finishOrder;

    private bool HasViceRoles => Seats.Count >= PresidentConstants.VICE_ROLES_MIN_PLAYERS;

    #region Dealing & exchange

    protected override async Task StartDealAsync()
    {
        OnStart();
        _randomService.ShuffleInPlace(Seats);
        RoundNumber = 1;
        await BeginRoundAsync();
    }

    /// <summary>
    /// Resets all per-round state, deals a fresh hand to everyone, then either opens the card
    /// exchange (from the second round onwards) or starts playing right away (first round).
    /// </summary>
    private async Task BeginRoundAsync()
    {
        ResetRoundState();
        DealCards();

        if (RoundNumber > 1)
        {
            ApplyAutomaticGifts();
            Phase = PresidentPhase.Exchange;
            await RenderAllAsync(resendPublic: true, resendLog: true);
            RestartTurnTimer();
            return;
        }

        await BeginPlayingAsync(Seats[0]);
    }

    /// <summary>
    /// Clears every piece of per-round state so a fresh hand can be dealt over the same seating.
    /// Roles and scores survive: they drive the exchange and the final standings.
    /// </summary>
    private void ResetRoundState()
    {
        foreach (var player in Seats)
        {
            player.Hand.Clear();
            player.FinishPosition = 0;
            player.HasPassed = false;
            player.CardsToGive = 0;
            player.PendingGives.Clear();
            player.ReceivedCards.Clear();
            player.GivenCards.Clear();
        }

        _finishOrder.Clear();
        _twoFinishers.Clear();
        CurrentTrick = new PresidentTrick();
        LastTrickWinner = null;
        _matchRequired = false;
    }

    /// <summary>
    /// Deals the whole 52-card deck out round-robin; with some player counts a few players end up
    /// holding one card more than the others, which is part of the game.
    /// </summary>
    private void DealCards()
    {
        var deck = PresidentConstants.BuildDeck();
        _randomService.ShuffleInPlace(deck);

        for (var cardIndex = 0; cardIndex < deck.Count; cardIndex++)
        {
            Seats[cardIndex % Seats.Count].Hand.Add(deck[cardIndex]);
        }

        foreach (var player in Seats)
        {
            SortHand(player.Hand);
        }
    }

    /// <summary>
    /// Applies the forced part of the exchange: the scum hands their best cards to the president
    /// (and the vice-scum their best card to the vice-president). The give-backs are then chosen
    /// freely by the receivers during the exchange phase.
    /// </summary>
    private void ApplyAutomaticGifts()
    {
        GiftBestCards(PresidentRole.Scum, PresidentRole.President, PresidentConstants.SCUM_EXCHANGE_COUNT);

        if (HasViceRoles)
        {
            GiftBestCards(PresidentRole.ViceScum, PresidentRole.VicePresident, PresidentConstants.VICE_EXCHANGE_COUNT);
        }
    }

    private void GiftBestCards(PresidentRole giverRole, PresidentRole receiverRole, int count)
    {
        var giver = FindByRole(giverRole);
        var receiver = FindByRole(receiverRole);
        if (giver is null || receiver is null)
        {
            return;
        }

        var bestCards = giver.Hand.OrderByDescending(card => card.Rank).Take(count).ToList();
        foreach (var card in bestCards)
        {
            giver.Hand.Remove(card);
            receiver.Hand.Add(card);
        }

        SortHand(receiver.Hand);
        receiver.ReceivedCards.AddRange(bestCards);
        receiver.CardsToGive = count;
        giver.GivenCards.AddRange(bestCards);

        LogEvent("president_exchange_gave_best", giver.Name, count, receiver.Name);
    }

    private PresidentPlayer FindByRole(PresidentRole role) =>
        Seats.FirstOrDefault(player => player.Role == role);

    public Task GiveAsync(IUser user, IReadOnlyList<PresidentCard> cards) =>
        RunActionAsync(() => GiveCoreAsync(user, cards));

    private async Task GiveCoreAsync(IUser user, IReadOnlyList<PresidentCard> cards)
    {
        if (Phase != PresidentPhase.Exchange || cards is null || cards.Count == 0)
        {
            return;
        }

        var player = FindSeat(user.UserId);
        if (player is null || player.CardsToGive == 0)
        {
            return;
        }

        // A full list (e.g. typed in one go) is applied directly; anything else toggles the selection.
        if (cards.Count == player.CardsToGive && cards.Distinct().Count() == cards.Count)
        {
            await ApplyGiveAsync(player, cards);
            return;
        }

        foreach (var card in cards)
        {
            if (!player.Hand.Contains(card))
            {
                Context.ReplyLocalizedMessage("president_give_not_in_hand");
                return;
            }

            if (!player.PendingGives.Remove(card) && player.PendingGives.Count < player.CardsToGive)
            {
                player.PendingGives.Add(card);
            }
        }

        if (player.PendingGives.Count == player.CardsToGive)
        {
            await ApplyGiveAsync(player, player.PendingGives.ToList());
            return;
        }

        await RenderPlayerPageAsync(player);
    }

    private async Task ApplyGiveAsync(PresidentPlayer giver, IReadOnlyList<PresidentCard> cards)
    {
        if (cards.Count != giver.CardsToGive
            || cards.Distinct().Count() != cards.Count
            || cards.Any(card => !giver.Hand.Contains(card)))
        {
            Context.ReplyLocalizedMessage("president_give_not_in_hand");
            return;
        }

        var receiver = FindByRole(giver.Role == PresidentRole.President
            ? PresidentRole.Scum
            : PresidentRole.ViceScum);
        if (receiver is null)
        {
            return;
        }

        foreach (var card in cards)
        {
            giver.Hand.Remove(card);
            receiver.Hand.Add(card);
        }

        SortHand(receiver.Hand);
        receiver.ReceivedCards.AddRange(cards);
        giver.PendingGives.Clear();
        giver.CardsToGive = 0;

        LogEvent("president_exchange_returned", giver.Name, cards.Count, receiver.Name);

        if (Seats.All(player => player.CardsToGive == 0))
        {
            // The scum of the previous round opens the new one.
            await BeginPlayingAsync(FindByRole(PresidentRole.Scum) ?? Seats[0]);
            return;
        }

        await RenderAllAsync();
    }

    #endregion

    #region Playing

    private async Task BeginPlayingAsync(PresidentPlayer leader)
    {
        Phase = PresidentPhase.Playing;
        CurrentTurnIndex = SeatIndexOf(leader);

        LogEvent("president_round_started", RoundNumber, TotalRounds, leader.Name);

        await RenderAllAsync(resendPublic: true, resendLog: true);
        RestartTurnTimer();
    }

    /// <summary>
    /// The (rank, card count) combinations the given player may legally put on the pile, or nothing at
    /// all when it is not their turn to act.
    /// </summary>
    public IReadOnlyList<(int Rank, int Count)> GetLegalPlays(PresidentPlayer player) =>
        Phase != PresidentPhase.Playing || player is null || CurrentPlayer != player
            ? []
            : PresidentRules.GetLegalPlays(player.Hand, CurrentTrick, _matchRequired);

    /// <summary>
    /// A player may pass only on their turn and never when leading a fresh pile.
    /// </summary>
    public bool CanPass(PresidentPlayer player) =>
        Phase == PresidentPhase.Playing && player is not null && CurrentPlayer == player && !CurrentTrick.IsEmpty;

    public Task PlayAsync(IUser user, int rank, int count) => RunActionAsync(() => PlayCoreAsync(user, rank, count));

    private async Task PlayCoreAsync(IUser user, int rank, int count)
    {
        if (Phase != PresidentPhase.Playing || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        var player = CurrentPlayer;
        if (count <= 0)
        {
            count = CurrentTrick.IsEmpty ? 1 : CurrentTrick.RequiredCount;
        }

        var matching = player.Hand.Where(card => card.Rank == rank).Take(count).ToList();
        if (matching.Count < count)
        {
            Context.ReplyLocalizedMessage("president_play_not_in_hand");
            return;
        }

        switch (PresidentRules.BeatsCurrentPlay(CurrentTrick, rank, count, _matchRequired))
        {
            case PresidentPlayRejection.WrongCount:
                Context.ReplyLocalizedMessage("president_play_wrong_count", CurrentTrick.RequiredCount);
                return;
            case PresidentPlayRejection.TooLow:
                Context.ReplyLocalizedMessage("president_play_too_low");
                return;
            case PresidentPlayRejection.MustMatch:
                Context.ReplyLocalizedMessage("president_play_must_match",
                    PresidentCard.DisplayRank(CurrentTrick.TopRank.Value, Context.Culture));
                return;
        }

        foreach (var card in matching)
        {
            player.Hand.Remove(card);
        }

        // "Ou rien": matching the rank of the previous play pins the next player to that exact rank.
        _matchRequired = !CurrentTrick.IsEmpty && rank == CurrentTrick.TopRank;
        CurrentTrick.Add(player, matching);

        var completedSquare = PresidentRules.CompletesSquare(CurrentTrick, rank);
        if (completedSquare)
        {
            LogEvent("president_square_closed", player.Name,
                PresidentCard.DisplayRank(rank, Context.Culture));
        }

        if (player.Hand.Count == 0)
        {
            RecordFinisher(player, rank);
        }

        if (await TryFinishRoundAsync())
        {
            return;
        }

        // The 2 is unbeatable and a completed square slams the pile shut: both take it on the spot.
        if (rank == PresidentCard.TWO || completedSquare)
        {
            await CloseTrickAsync(player);
            return;
        }

        await AdvanceTurnAsync(player);
    }

    /// <summary>
    /// Books a player who has just emptied their hand into the finish order. Going out on a 2 relegates
    /// them to the bottom instead: they are kept out of the regular order and appended last when the
    /// round ends. Their display position is provisional until then; a later offender sinks even lower.
    /// </summary>
    private void RecordFinisher(PresidentPlayer player, int rank)
    {
        if (rank != PresidentCard.TWO)
        {
            _finishOrder.Add(player);
            player.FinishPosition = _finishOrder.Count;
            LogEvent("president_player_finished", player.Name, player.FinishPosition);
            return;
        }

        _twoFinishers.Add(player);
        for (var offenderIndex = 0; offenderIndex < _twoFinishers.Count; offenderIndex++)
        {
            _twoFinishers[offenderIndex].FinishPosition =
                Seats.Count - _twoFinishers.Count + 1 + offenderIndex;
        }

        LogEvent("president_finished_on_two", player.Name);
    }

    public Task PassAsync(IUser user) => RunActionAsync(() => PassCoreAsync(user));

    private async Task PassCoreAsync(IUser user)
    {
        if (Phase != PresidentPhase.Playing || CurrentPlayer?.UserId != user.UserId)
        {
            return;
        }

        if (CurrentTrick.IsEmpty)
        {
            Context.ReplyLocalizedMessage("president_pass_leading");
            return;
        }

        // "Ou rien": declining to match only skips this turn, the player stays in the trick.
        if (_matchRequired)
        {
            _matchRequired = false;
            LogEvent("president_ou_rien_skipped", CurrentPlayer.Name);
            await AdvanceTurnAsync(CurrentTrick.LastPlayer);
            return;
        }

        CurrentPlayer.HasPassed = true;
        await AdvanceTurnAsync(CurrentTrick.LastPlayer);
    }

    /// <summary>
    /// Moves the turn to the next player still holding cards who has not passed on this pile. When
    /// the turn would come back to the author of the top play (everyone else passed or is out), the
    /// pile is taken instead. A player pinned by the "ou rien" rule who cannot match the rank is
    /// skipped outright, which lifts the constraint for the player after them.
    /// </summary>
    private async Task AdvanceTurnAsync(PresidentPlayer lastPlayer)
    {
        for (var offset = 1; offset <= Seats.Count; offset++)
        {
            var candidate = SeatFrom(CurrentTurnIndex, offset);
            if (candidate == lastPlayer)
            {
                break;
            }

            if (candidate.Hand.Count == 0 || candidate.HasPassed)
            {
                continue;
            }

            if (_matchRequired && !PresidentRules.CanMatchPile(candidate.Hand, CurrentTrick))
            {
                _matchRequired = false;
                LogEvent("president_ou_rien_skipped", candidate.Name);
                continue;
            }

            CurrentTurnIndex = SeatIndexOf(candidate);
            await RenderAllAsync();
            RestartTurnTimer();
            return;
        }

        await CloseTrickAsync(lastPlayer);
    }

    /// <summary>
    /// Clears the pile and hands the lead to its winner, or to the next player still holding cards
    /// when the winner just emptied their hand.
    /// </summary>
    private async Task CloseTrickAsync(PresidentPlayer winner)
    {
        foreach (var player in Seats)
        {
            player.HasPassed = false;
        }

        CurrentTrick = new PresidentTrick();
        LastTrickWinner = winner;
        _matchRequired = false;
        LogEvent("president_trick_won", winner.Name);

        var winnerIndex = SeatIndexOf(winner);
        for (var offset = 0; offset < Seats.Count; offset++)
        {
            var candidate = SeatFrom(winnerIndex, offset);
            if (candidate.Hand.Count > 0)
            {
                CurrentTurnIndex = SeatIndexOf(candidate);
                break;
            }
        }

        // Force re-post the public chat panel so it drops back to the bottom of the chat instead of
        // staying stuck high up in the scrollback. Player hands live in HTML pages that update in
        // place, so they need no such workaround.
        await RenderAllAsync(resendPublic: true, resendLog: true);
        RestartTurnTimer();
    }

    #endregion

    #region Round end & scoring

    /// <summary>
    /// Ends the round once at most one player still holds cards: that player takes the last place,
    /// roles and points are handed out, and either the next round starts or the game finishes.
    /// </summary>
    private async Task<bool> TryFinishRoundAsync()
    {
        var stillPlaying = Seats.Where(player => player.Hand.Count > 0).ToList();
        if (stillPlaying.Count > 1)
        {
            return false;
        }

        if (stillPlaying.Count == 1)
        {
            _finishOrder.Add(stillPlaying[0]);
        }

        // Whoever went out on a 2 sinks below even the player left holding cards.
        _finishOrder.AddRange(_twoFinishers);
        _twoFinishers.Clear();

        AssignRolesAndPoints();
        LogEvent("president_round_ended", RoundNumber);

        if (RoundNumber >= TotalRounds)
        {
            await FinishGameAsync();
            return true;
        }

        RoundNumber++;
        await BeginRoundAsync();
        return true;
    }

    private void AssignRolesAndPoints()
    {
        for (var position = 1; position <= _finishOrder.Count; position++)
        {
            var player = _finishOrder[position - 1];
            player.FinishPosition = position;
            player.Role = RoleForPosition(position, _finishOrder.Count);
            player.Score += _finishOrder.Count - position;

            if (player.Role != PresidentRole.Neutral)
            {
                LogEvent("president_role_earned", player.Name,
                    Context.GetString($"president_role_{player.Role.ToString().ToLowerInvariant()}"));
            }
        }
    }

    private PresidentRole RoleForPosition(int position, int playerCount)
    {
        if (position == 1)
        {
            return PresidentRole.President;
        }

        if (position == playerCount)
        {
            return PresidentRole.Scum;
        }

        if (HasViceRoles && position == 2)
        {
            return PresidentRole.VicePresident;
        }

        if (HasViceRoles && position == playerCount - 1)
        {
            return PresidentRole.ViceScum;
        }

        return PresidentRole.Neutral;
    }

    private async Task FinishGameAsync()
    {
        Phase = PresidentPhase.Finished;
        StopTurnTimer();
        ClearSubPanel();
        await RenderAllAsync();
        OnEnd();
    }

    #endregion

    #region Timeouts

    protected override async Task OnTurnTimeoutAsync()
    {
        switch (Phase)
        {
            case PresidentPhase.Exchange:
                await AutoGiveAsync();
                break;
            case PresidentPhase.Playing when CurrentPlayer is not null:
                if (CurrentTrick.IsEmpty)
                {
                    var (rank, count) = GetLegalPlays(CurrentPlayer)[0];
                    await PlayCoreAsync(CurrentPlayer.User, rank, count);
                }
                else
                {
                    await PassCoreAsync(CurrentPlayer.User);
                }

                break;
        }
    }

    /// <summary>
    /// Resolves the exchange for every player who let their give-back time out, handing over their
    /// lowest cards.
    /// </summary>
    private async Task AutoGiveAsync()
    {
        foreach (var player in Seats.Where(currentPlayer => currentPlayer.CardsToGive > 0).ToList())
        {
            var lowestCards = player.Hand.OrderBy(card => card.Rank).Take(player.CardsToGive).ToList();
            await ApplyGiveAsync(player, lowestCards);
        }
    }

    /// <summary>
    /// While playing, only the player on turn is warned; during the exchange every player who still
    /// owes cards is.
    /// </summary>
    protected override IEnumerable<PresidentPlayer> GetTurnWarningRecipients() => Phase switch
    {
        PresidentPhase.Exchange => Seats.Where(player => player.CardsToGive > 0).ToList(),
        PresidentPhase.Playing when CurrentPlayer is not null => [CurrentPlayer],
        _ => []
    };

    #endregion

    protected override PresidentViewModel BuildModel(PresidentPlayer viewer) => new()
    {
        Culture = Context.Culture,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        RoomId = Context.RoomId,
        Game = this,
        Viewer = viewer,
        ViewerHand = viewer?.Hand ?? [],
        ViewerLegalPlays = GetLegalPlays(viewer),
        ViewerCanPass = CanPass(viewer)
    };

    private static void SortHand(List<PresidentCard> hand)
    {
        hand.Sort((first, second) =>
        {
            var rankComparison = first.Rank.CompareTo(second.Rank);
            return rankComparison != 0 ? rankComparison : first.Suit.CompareTo(second.Suit);
        });
    }
}
