namespace ElsaMina.Core.Services.Http;

/// <summary>
/// Turns a request body into the <see cref="HttpContent"/> to send
/// </summary>
public interface IHttpRequestBody
{
    HttpContent CreateContent();
}
