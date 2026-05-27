using System.Net;
using System.Net.Sockets;
using ElsaMina.Core.Services.Telemetry;
using Newtonsoft.Json;

namespace ElsaMina.Core.Services.Http;

public class HttpService : IHttpService
{
    private const string DEFAULT_USER_AGENT =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;

    public HttpService(ITelemetryService telemetryService)
    {
        // Pour l'ipv6 et le manque de support du happy eyeballs côté client http .NET.
        var socketsHandler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                await socket.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };

        var pipeline = new TelemetryHttpHandler(telemetryService)
        {
            InnerHandler = socketsHandler
        };

        _httpClient = new HttpClient(pipeline)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", DEFAULT_USER_AGENT);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
    }

    public async Task<IHttpResponse<TResponse>> SendAsync<TResponse>(HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAndEnsureSuccessAsync(request, cancellationToken);
        var content = await ReadTransformedContentAsync(request, response, cancellationToken);
        return new HttpResponse<TResponse>
        {
            StatusCode = response.StatusCode,
            Data = JsonConvert.DeserializeObject<TResponse>(content)
        };
    }

    public async Task<IHttpResponse<string>> SendForStringAsync(HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAndEnsureSuccessAsync(request, cancellationToken);
        var content = await ReadTransformedContentAsync(request, response, cancellationToken);
        return new HttpResponse<string>
        {
            StatusCode = response.StatusCode,
            Data = content
        };
    }

    public async Task<Stream> SendForStreamAsync(HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAndEnsureSuccessAsync(request, cancellationToken);
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAndEnsureSuccessAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var requestMessage = BuildRequestMessage(request);
        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpException(response.StatusCode, errorContent);
    }

    private static async Task<string> ReadTransformedContentAsync(HttpRequest request,
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (request.SkipFirstResponseCharacter && content.Length > 0)
        {
            content = content[1..];
        }

        return content;
    }

    private static HttpRequestMessage BuildRequestMessage(HttpRequest request)
    {
        var requestMessage = new HttpRequestMessage(request.Method, BuildUri(request))
        {
            Content = request.Body?.CreateContent()
        };

        foreach (var header in request.Headers)
        {
            requestMessage.Headers.Add(header.Key, header.Value);
        }

        return requestMessage;
    }

    private static string BuildUri(HttpRequest request)
    {
        if (request.QueryParameters.Count == 0)
        {
            return request.Uri;
        }

        var query = string.Join("&", request.QueryParameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        return $"{request.Uri}?{query}";
    }
}
