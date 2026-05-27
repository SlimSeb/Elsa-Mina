using System.Net;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Login;
using ElsaMina.Core.Services.System;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Core.Services.Login;

public class LoginServiceTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private ISystemService _systemService;
    private LoginService _loginService;

    private const string CHALLSTR = "sampleChallstr";
    private const string USERNAME = "User";
    private const string USER_ID = "user";

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _systemService = Substitute.For<ISystemService>();
        _configuration.Name.Returns(USERNAME);
        _configuration.Password.Returns("password");
        _configuration.LoginRetryDelay.Returns(TimeSpan.FromMilliseconds(1));
        _loginService = new LoginService(_httpService, _configuration, _systemService);
    }

    private static string ReadBody(HttpRequest request) =>
        request.Body.CreateContent().ReadAsStringAsync().GetAwaiter().GetResult();

    private static IHttpResponse<LoginResponseDto> MakeSuccessResponse() =>
        new HttpResponse<LoginResponseDto>
        {
            Data = new LoginResponseDto
            {
                CurrentUser = new CurrentUserDto { UserId = USER_ID, Username = USERNAME },
                Assertion = "assertion-token"
            }
        };

    [Test]
    public async Task Test_Login_ShouldReturnLoginResponse_WhenLoginSucceedsOnFirstAttempt()
    {
        // Arrange
        _httpService
            .SendAsync<LoginResponseDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeSuccessResponse());

        // Act
        var result = await _loginService.Login(CHALLSTR);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.CurrentUser.UserId, Is.EqualTo(USER_ID));
        }
    }

    [Test]
    public async Task Test_Login_ShouldSendCorrectFormData()
    {
        // Arrange
        _httpService
            .SendAsync<LoginResponseDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeSuccessResponse());

        // Act
        await _loginService.Login(CHALLSTR);

        // Assert
        await _httpService.Received().SendAsync<LoginResponseDto>(
            Arg.Is<HttpRequest>(request =>
                request.Uri == LoginService.LOGIN_URL &&
                ReadBody(request).Contains($"challstr={CHALLSTR}") &&
                ReadBody(request).Contains($"name={USERNAME}") &&
                ReadBody(request).Contains("pass=password") &&
                ReadBody(request).Contains("act=login")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Login_ShouldRetry_WhenHttpExceptionThrown()
    {
        // Arrange
        _httpService
            .SendAsync<LoginResponseDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new HttpException(HttpStatusCode.InternalServerError, "Server Error"),
                _ => MakeSuccessResponse());

        // Act
        var result = await _loginService.Login(CHALLSTR);

        // Assert
        Assert.That(result, Is.Not.Null);
        await _httpService.Received(2).SendAsync<LoginResponseDto>(
            Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
        await _systemService.Received(1).SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Login_ShouldRetry_WhenGeneralExceptionThrown()
    {
        // Arrange
        _httpService
            .SendAsync<LoginResponseDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new Exception("Timeout"),
                _ => MakeSuccessResponse());

        // Act
        var result = await _loginService.Login(CHALLSTR);

        // Assert
        Assert.That(result, Is.Not.Null);
        await _httpService.Received(2).SendAsync<LoginResponseDto>(
            Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
        await _systemService.Received(1).SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_Login_ShouldRetry_WhenUserIdMismatch()
    {
        // Arrange
        var mismatchResponse = new HttpResponse<LoginResponseDto>
        {
            Data = new LoginResponseDto
            {
                CurrentUser = new CurrentUserDto { UserId = "wronguser", Username = "WrongUser" }
            }
        };
        _httpService
            .SendAsync<LoginResponseDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(mismatchResponse, MakeSuccessResponse());

        // Act
        var result = await _loginService.Login(CHALLSTR);

        // Assert
        Assert.That(result.CurrentUser.UserId, Is.EqualTo(USER_ID));
        await _httpService.Received(2).SendAsync<LoginResponseDto>(
            Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
        await _systemService.Received(1).SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}
