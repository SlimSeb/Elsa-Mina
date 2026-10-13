using System.Net;
using System.Net.Sockets;
using System.Text;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Telemetry;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Http;

// Sends real requests to a local HTTP server
public class HttpServiceTest
{
    private HttpListener _listener;
    private string _baseUrl;
    private HttpStatusCode _responseStatus;
    private string _responseBody;
    private string _receivedMethod;
    private string _receivedUrl;
    private string _receivedBody;
    private string _receivedContentType;
    private WebHeaderCollection _receivedHeaders;
    private ITelemetryService _telemetryService;
    private HttpService _service;

    [SetUp]
    public void SetUp()
    {
        var port = GetFreePort();
        _baseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseUrl);
        _listener.Start();
        _ = ServeAsync(_listener);

        _responseStatus = HttpStatusCode.OK;
        _responseBody = "";
        _telemetryService = Substitute.For<ITelemetryService>();
        _service = new HttpService(_telemetryService);
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Close();
    }

    private static int GetFreePort()
    {
        var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }

    private async Task ServeAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            _receivedMethod = context.Request.HttpMethod;
            _receivedUrl = context.Request.Url?.PathAndQuery;
            _receivedHeaders = (WebHeaderCollection)context.Request.Headers;
            _receivedContentType = context.Request.ContentType;
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                _receivedBody = await reader.ReadToEndAsync();
            }

            context.Response.StatusCode = (int)_responseStatus;
            var bytes = Encoding.UTF8.GetBytes(_responseBody);
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    [Test]
    public async Task Test_SendAsync_ShouldDeserializeJsonResponse()
    {
        // Arrange
        _responseBody = """{"name":"Elsa","count":"3","status":"NotFound"}""";

        // Act
        var response = await _service.SendAsync<HttpServiceTestPayload>(HttpRequest.Get(_baseUrl + "data"));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Data.Name, Is.EqualTo("Elsa"));
            Assert.That(response.Data.Count, Is.EqualTo(3));
            Assert.That(response.Data.Status, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(_receivedMethod, Is.EqualTo("GET"));
            Assert.That(_receivedUrl, Is.EqualTo("/data"));
        }
        _telemetryService.Received(1).RecordHttpRequest("GET", "127.0.0.1", 200);
    }

    [Test]
    public async Task Test_SendAsync_ShouldReturnDefault_WhenResponseIsEmpty()
    {
        // Act
        var response = await _service.SendAsync<HttpServiceTestPayload>(HttpRequest.Get(_baseUrl));

        // Assert
        Assert.That(response.Data, Is.Null);
    }

    [Test]
    public async Task Test_SendAsync_ShouldSkipFirstCharacter_WhenRequested()
    {
        // Arrange
        _responseBody = """]{"name":"Elsa"}""";

        // Act
        var response = await _service.SendAsync<HttpServiceTestPayload>(
            HttpRequest.Get(_baseUrl).SkippingFirstResponseCharacter());

        // Assert
        Assert.That(response.Data.Name, Is.EqualTo("Elsa"));
    }

    [Test]
    public async Task Test_SendForStringAsync_ShouldSendEscapedQueryParametersAndHeaders()
    {
        // Arrange
        _responseBody = "plain text";

        // Act
        var response = await _service.SendForStringAsync(HttpRequest.Get(_baseUrl + "search")
            .WithQueryParameter("q", "a b&c")
            .WithQueryParameters(new Dictionary<string, string> { ["page"] = "2" })
            .WithHeader("X-Custom", "value")
            .WithHeaders(new Dictionary<string, string> { ["X-Other"] = "other" }));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Data, Is.EqualTo("plain text"));
            Assert.That(_receivedUrl, Is.EqualTo("/search?q=a%20b%26c&page=2"));
            Assert.That(_receivedHeaders["X-Custom"], Is.EqualTo("value"));
            Assert.That(_receivedHeaders["X-Other"], Is.EqualTo("other"));
            Assert.That(_receivedHeaders["User-Agent"], Does.StartWith("Mozilla/5.0"));
        }
    }

    [Test]
    public async Task Test_SendForStringAsync_ShouldSendJsonBody()
    {
        // Act
        await _service.SendForStringAsync(HttpRequest.Post(_baseUrl).WithJsonBody(new { name = "Elsa" }));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_receivedMethod, Is.EqualTo("POST"));
            Assert.That(_receivedBody, Is.EqualTo("""{"name":"Elsa"}"""));
            Assert.That(_receivedContentType, Does.StartWith("application/json"));
        }
    }

    [Test]
    public async Task Test_SendForStringAsync_ShouldSendNullJsonBody_WhenPayloadIsNull()
    {
        // Act
        await _service.SendForStringAsync(HttpRequest.For(HttpMethod.Put, _baseUrl).WithJsonBody(null));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_receivedMethod, Is.EqualTo("PUT"));
            Assert.That(_receivedBody, Is.EqualTo("null"));
        }
    }

    [Test]
    public async Task Test_SendForStringAsync_ShouldSendFormBody()
    {
        // Act
        await _service.SendForStringAsync(HttpRequest.Post(_baseUrl).WithFormBody(
            new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["id"] = "a b" }));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_receivedBody, Is.EqualTo("grant_type=client_credentials&id=a+b"));
            Assert.That(_receivedContentType, Is.EqualTo("application/x-www-form-urlencoded"));
        }
    }

    [Test]
    public async Task Test_SendForStreamAsync_ShouldReturnResponseBodyStream()
    {
        // Arrange
        _responseBody = "binary";

        // Act
        await using var stream = await _service.SendForStreamAsync(HttpRequest.Get(_baseUrl));
        using var reader = new StreamReader(stream);

        // Assert
        Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("binary"));
    }

    [Test]
    public void Test_SendAsync_ShouldThrowHttpException_WhenResponseIsNotSuccessful()
    {
        // Arrange
        _responseStatus = HttpStatusCode.NotFound;
        _responseBody = "missing";

        // Act
        var exception = Assert.ThrowsAsync<HttpException>(() =>
            _service.SendAsync<HttpServiceTestPayload>(HttpRequest.Get(_baseUrl)));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(exception.ResponseContent, Is.EqualTo("missing"));
            Assert.That(exception.Message, Is.EqualTo("HttpException: missing (NotFound)"));
        }
        _telemetryService.Received(1).RecordHttpRequest("GET", "127.0.0.1", 404);
    }

    [Test]
    public void Test_SendForStringAsync_ShouldThrow_WhenServerIsUnreachable()
    {
        // Arrange
        _listener.Close();

        // Act & Assert
        Assert.CatchAsync<HttpRequestException>(() => _service.SendForStringAsync(HttpRequest.Get(_baseUrl)));
    }
}
