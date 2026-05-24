using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Logging;

namespace ElsaMina.Core.Handlers;

public class HandlerManager : IHandlerManager
{
    private readonly IDependencyContainerService _containerService;
    private readonly ITelemetryService _telemetryService;

    private readonly ConcurrentDictionary<string, IHandler> _handlers = [];

    public HandlerManager(IDependencyContainerService containerService, ITelemetryService telemetryService)
    {
        _containerService = containerService;
        _telemetryService = telemetryService;
    }

    public bool IsInitialized { get; private set; }

    public void Initialize()
    {
        var handlers = _containerService.Resolve<IEnumerable<IHandler>>().ToList();
        foreach (var handler in handlers)
        {
            _handlers[handler.Identifier] = handler;
        }

        IsInitialized = true;
    }

    public async Task HandleMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        var messageType = parts.Length > 1 ? parts[1] : null;
        var adequateHandlers = _handlers
            .Values
            .Where(handler => IsHandlerAdequate(handler, messageType));

        await Task.WhenAll(
            adequateHandlers.Select(handler => TryHandleMessageAsync(parts, roomId, handler, cancellationToken))
        );
    }

    private static bool IsHandlerAdequate(IHandler handler, string messageType)
    {
        return handler.IsEnabled
               && (handler.HandledMessageTypes == null ||
                   (messageType != null && handler.HandledMessageTypes.Contains(messageType)));
    }

    private async Task TryHandleMessageAsync(string[] parts, string roomId, IHandler handler,
        CancellationToken cancellationToken)
    {
        using var activity = _telemetryService.StartActivity($"handler.{handler.Identifier}");
        activity?.SetTag("room", roomId);
        try
        {
            await handler.HandleReceivedMessageAsync(parts, roomId, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            Log.Error(exception, "Error while handling message in handler {Handler}", handler.Identifier);
            throw;
        }
    }
}