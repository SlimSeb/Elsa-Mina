namespace ElsaMina.Core.Services.Http;

/// <summary>
/// Transforme le corps d'une requête en <see cref="HttpContent"/> à envoyer
/// </summary>
public interface IHttpRequestBody
{
    HttpContent CreateContent();
}
