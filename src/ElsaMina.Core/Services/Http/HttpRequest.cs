namespace ElsaMina.Core.Services.Http;

public sealed class HttpRequest
{
    private readonly Dictionary<string, string> _queryParameters = new();
    private readonly Dictionary<string, string> _headers = new();

    private HttpRequest(HttpMethod method, string uri)
    {
        Method = method;
        Uri = uri;
    }

    public HttpMethod Method { get; }
    public string Uri { get; }
    public IReadOnlyDictionary<string, string> QueryParameters => _queryParameters;
    public IReadOnlyDictionary<string, string> Headers => _headers;
    public IHttpRequestBody Body { get; private set; }

    /// <remarks>
    /// Enlève le premier caractère du contenu de la réponse avant déserialiser. Certains
    /// endpoints (comme ceux de PS) ajoutent un caractère inutile devant
    /// </remarks>
    public bool SkipFirstResponseCharacter { get; private set; }

    public static HttpRequest Get(string uri) => new(HttpMethod.Get, uri);

    public static HttpRequest Post(string uri) => new(HttpMethod.Post, uri);

    public static HttpRequest For(HttpMethod method, string uri) => new(method, uri);

    public HttpRequest WithQueryParameter(string key, string value)
    {
        _queryParameters[key] = value;
        return this;
    }

    public HttpRequest WithQueryParameters(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        if (parameters == null)
        {
            return this;
        }

        foreach (var parameter in parameters)
        {
            _queryParameters[parameter.Key] = parameter.Value;
        }

        return this;
    }

    public HttpRequest WithHeader(string key, string value)
    {
        _headers[key] = value;
        return this;
    }

    public HttpRequest WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
    {
        if (headers == null)
        {
            return this;
        }

        foreach (var header in headers)
        {
            _headers[header.Key] = header.Value;
        }

        return this;
    }

    public HttpRequest WithJsonBody(object payload)
    {
        Body = new JsonRequestBody(payload);
        return this;
    }

    public HttpRequest WithFormBody(IEnumerable<KeyValuePair<string, string>> fields)
    {
        Body = new FormUrlEncodedRequestBody(fields);
        return this;
    }

    public HttpRequest WithBody(IHttpRequestBody body)
    {
        Body = body;
        return this;
    }

    public HttpRequest SkippingFirstResponseCharacter()
    {
        SkipFirstResponseCharacter = true;
        return this;
    }
}
