using System.Text;
using Newtonsoft.Json;

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
        var serializedJson = JsonConvert.SerializeObject(_payload);
        return new StringContent(serializedJson, Encoding.UTF8, "application/json");
    }
}
