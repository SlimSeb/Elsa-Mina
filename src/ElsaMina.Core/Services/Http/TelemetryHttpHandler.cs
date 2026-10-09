using System.Diagnostics;
using ElsaMina.Core.Services.Telemetry;

namespace ElsaMina.Core.Services.Http;

/// <summary>
/// Étape du pipeline qui ajoute les métriques à chaque requête sortante
/// </summary>
public sealed class TelemetryHttpHandler : DelegatingHandler
{
    private readonly ITelemetryService _telemetryService;

    public TelemetryHttpHandler(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var method = request.Method.Method;
        var host = request.RequestUri?.Host ?? string.Empty;

        using var activity = _telemetryService.StartActivity($"{method} {host}");
        activity?.SetTag("http.request.method", method);
        activity?.SetTag("server.address", host);
        activity?.SetTag("url.full", request.RequestUri?.ToString());

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            var statusCode = (int)response.StatusCode;

            activity?.SetTag("http.response.status_code", statusCode);
            if (!response.IsSuccessStatusCode)
            {
                activity?.SetStatus(ActivityStatusCode.Error, response.ReasonPhrase);
            }

            _telemetryService.RecordHttpRequest(method, host, statusCode);
            _telemetryService.RecordHttpRequestDuration(stopwatch.Elapsed.TotalMilliseconds, method, host);

            return response;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }
    }
}
