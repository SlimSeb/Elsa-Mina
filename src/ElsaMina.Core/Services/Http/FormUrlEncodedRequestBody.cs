namespace ElsaMina.Core.Services.Http;

public sealed class FormUrlEncodedRequestBody : IHttpRequestBody
{
    private readonly IEnumerable<KeyValuePair<string, string>> _fields;

    public FormUrlEncodedRequestBody(IEnumerable<KeyValuePair<string, string>> fields)
    {
        _fields = fields;
    }

    public HttpContent CreateContent() => new FormUrlEncodedContent(_fields);
}
