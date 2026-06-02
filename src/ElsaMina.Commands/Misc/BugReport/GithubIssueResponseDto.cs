using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.BugReport;

public class GithubIssueResponseDto
{
    [JsonProperty("number")]
    public int Number { get; set; }

    [JsonProperty("html_url")]
    public string HtmlUrl { get; set; }
}
