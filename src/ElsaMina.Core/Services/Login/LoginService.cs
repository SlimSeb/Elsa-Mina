using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Core.Services.Login;

public class LoginService : ILoginService
{
    public const string LOGIN_URL = "https://play.pokemonshowdown.com/action.php";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly ISystemService _systemService;

    public LoginService(IHttpService httpService, IConfiguration configuration, ISystemService systemService)
    {
        _httpService = httpService;
        _configuration = configuration;
        _systemService = systemService;
    }

    public async Task<LoginResponseDto> Login(string challstr, CancellationToken cancellationToken = default)
    {
        var expectedUserId = _configuration.Name.ToLowerAlphaNum();
        var form = new Dictionary<string, string>
        {
            ["challstr"] = challstr,
            ["name"] = _configuration.Name,
            ["pass"] = _configuration.Password,
            ["act"] = "login"
        };

        while (true)
        {
            try
            {
                Log.Information("Logging in...");
                var response = await _httpService.SendAsync<LoginResponseDto>(
                    HttpRequest.Post(LOGIN_URL).WithFormBody(form).SkippingFirstResponseCharacter(),
                    cancellationToken);

                if (response.Data?.CurrentUser != null && expectedUserId == response.Data.CurrentUser.UserId)
                {
                    return response.Data;
                }

                Log.Warning("Login response invalid or user mismatch");
            }
            catch (HttpException exception)
            {
                Log.Error("Login failed with status code {0} : {1}",
                    exception.StatusCode, exception.ResponseContent);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Login failed");
            }

            Log.Warning("Retrying login in {0}s...", _configuration.LoginRetryDelay.TotalSeconds);
            await _systemService.SleepAsync(_configuration.LoginRetryDelay, cancellationToken);
        }
    }
}