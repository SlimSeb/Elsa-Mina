using ElsaMina.Core.Services.UserData;

namespace ElsaMina.Battles;

public interface ILadderRatingService
{
    /// <summary>
    /// The bot's ladder ratings now, by format id, or null when Showdown could not be reached.
    /// </summary>
    Task<IReadOnlyDictionary<string, UserDataRankingDto>> GetRatingsAsync(CancellationToken cancellationToken = default);
}
