using System.Timers;
using ElsaMina.Logging;
using Timer = System.Timers.Timer;

namespace ElsaMina.Core.Services.Repeats;

public sealed class Repeat : IRepeat
{
    private readonly IBot _bot;
    private Timer _timer;

    public Repeat(IBot bot, Guid repeatId, string roomId, string message, TimeSpan interval)
    {
        _bot = bot;
        RepeatId = repeatId;
        RoomId = roomId;
        Message = message;
        Interval = interval;
    }

    public string RoomId { get; }
    public Guid RepeatId { get; }
    public string Message { get; }
    public TimeSpan Interval { get; }

    public void Start()
    {
        CancelTimer();
        _timer = new Timer(Interval);
        _timer.AutoReset = true;
        _timer.Elapsed += HandleTimerElapsed;
        _timer.Start();
    }

    public void Stop()
    {
        CancelTimer();
    }

    private void CancelTimer()
    {
        if (_timer == null)
        {
            return;
        }

        _timer.Elapsed -= HandleTimerElapsed;
        _timer.Dispose();
        _timer = null;
    }

    private void HandleTimerElapsed(object sender, ElapsedEventArgs e)
    {
        try
        {
            var prefix = Message.StartsWith("/wall") || Message.StartsWith("/announce") ? string.Empty : "[[]]";
            _bot.Say(RoomId, $"{prefix}{Message}");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Repeat {0} failed to send message in room {1}", RepeatId, RoomId);
        }
    }
}
