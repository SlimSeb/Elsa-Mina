namespace ElsaMina.Commands.Alerts;

/// <param name="ChannelId">Stable identifier used to query the platform.</param>
/// <param name="ChannelName">Readable name (login, handle, nickname) used for display and removal.</param>
public record AlertChannel(string ChannelId, string ChannelName);
