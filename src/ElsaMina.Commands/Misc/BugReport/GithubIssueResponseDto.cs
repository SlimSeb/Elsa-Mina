using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.BugReport;

public class GithubIssueResponseDto
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; }
}
