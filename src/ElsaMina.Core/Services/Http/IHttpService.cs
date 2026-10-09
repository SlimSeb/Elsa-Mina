namespace ElsaMina.Core.Services.Http;

public interface IHttpService
{
    /// <summary>
    /// Envoie la requête et désérialise le corps JSON de la réponse en <typeparamref name="TResponse"/>.
    /// </summary>
    Task<IHttpResponse<TResponse>> SendAsync<TResponse>(HttpRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envoie la requête et renvoie le corps de la réponse en texte brut, sans désérialisation.
    /// </summary>
    Task<IHttpResponse<string>> SendForStringAsync(HttpRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envoie la requête et renvoie le corps de la réponse sous forme de flux (par exemple pour un téléchargement binaire).
    /// </summary>
    Task<Stream> SendForStreamAsync(HttpRequest request,
        CancellationToken cancellationToken = default);
}
