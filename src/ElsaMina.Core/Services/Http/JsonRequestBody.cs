using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ElsaMina.Core.Services.Http;

public sealed class JsonRequestBody : IHttpRequestBody
{
    private readonly object _payload;
    private readonly JsonTypeInfo _typeInfo;

    public JsonRequestBody(object payload)
    {
        _payload = payload;
    }

    public JsonRequestBody(object payload, JsonTypeInfo typeInfo)
    {
        _payload = payload;
        _typeInfo = typeInfo;
    }

    public HttpContent CreateContent()
    {
        if (_payload == null)
        {
            return new StringContent("null", Encoding.UTF8, "application/json");
        }

        if (_typeInfo != null)
        {
            return new StringContent(JsonSerializer.Serialize(_payload, _typeInfo), Encoding.UTF8, "application/json");
        }

        var resolvedTypeInfo = HttpService.DefaultJsonOptions.GetTypeInfo(_payload.GetType())
            ?? throw new InvalidOperationException($"Type {_payload.GetType().FullName} is not registered in source-generated serializer contexts.");
        return new StringContent(JsonSerializer.Serialize(_payload, resolvedTypeInfo), Encoding.UTF8, "application/json");
    }
}
