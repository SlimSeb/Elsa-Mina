namespace ElsaMina.Core.Services.Http;

public interface IHttpService
{
    /// <summary>
    /// Sends the request and deserializes the JSON response body into <typeparamref name="TResponse"/>.
    /// </summary>
    Task<IHttpResponse<TResponse>> SendAsync<TResponse>(HttpRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the request and returns the response body as plain text, without deserializing it.
    /// </summary>
    Task<IHttpResponse<string>> SendForStringAsync(HttpRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the request and returns the response body as a stream (for a binary download, for example).
    /// </summary>
    Task<Stream> SendForStreamAsync(HttpRequest request,
        CancellationToken cancellationToken = default);
}
