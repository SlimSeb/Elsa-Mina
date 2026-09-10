using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.BugReport;

public class GithubIssueRequestDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("labels")]
    public IEnumerable<string> Labels { get; set; }
}
