using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.UserData;

namespace ElsaMina.Battles;

public class LadderRatingService : ILadderRatingService
{
    private readonly IUserDataService _userDataService;
    private readonly IConfiguration _configuration;

    public LadderRatingService(IUserDataService userDataService, IConfiguration configuration)
    {
        _userDataService = userDataService;
        _configuration = configuration;
    }

    public async Task<IReadOnlyDictionary<string, UserDataRankingDto>> GetRatingsAsync(
        CancellationToken cancellationToken = default)
    {
        var userData = await _userDataService.GetLatestUserDataAsync(_configuration.Name, cancellationToken);
        if (userData == null)
        {
            return null;
        }

        return userData.Ratings == null
            ? new Dictionary<string, UserDataRankingDto>()
            : new Dictionary<string, UserDataRankingDto>(userData.Ratings);
    }
}
