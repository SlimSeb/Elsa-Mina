using System.Text;
using System.Text.Json;

namespace ElsaMina.Core.Services.Http;

public sealed class JsonRequestBody : IHttpRequestBody
{
    private readonly object _payload;

    public JsonRequestBody(object payload)
    {
        _payload = payload;
    }

    public HttpContent CreateContent()
    {
        var serializedJson = _payload != null
            ? JsonSerializer.Serialize(_payload, _payload.GetType())
            : "null";
        return new StringContent(serializedJson, Encoding.UTF8, "application/json");
    }
}
