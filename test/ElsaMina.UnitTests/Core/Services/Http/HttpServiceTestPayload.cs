using System.Net;

namespace ElsaMina.UnitTests.Core.Services.Http;

public sealed class HttpServiceTestPayload
{
    public string Name { get; set; }
    public int Count { get; set; }
    public HttpStatusCode Status { get; set; }
}
