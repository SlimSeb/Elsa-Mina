namespace ElsaMina.Commands.Alerts.Youtube;

public enum YoutubeVideoState
{
    /// <summary>
    /// Live stream or premiere scheduled but not started yet : it must be checked again.
    /// </summary>
    Upcoming,

    /// <summary>
    /// Already handled (announced or deliberately ignored).
    /// </summary>
    Handled
}
