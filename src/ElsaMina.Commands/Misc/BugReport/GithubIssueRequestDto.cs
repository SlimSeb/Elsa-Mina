using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.BugReport;

public class GithubIssueRequestDto
{
    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("body")]
    public string Body { get; set; }

    [JsonProperty("labels")]
    public IEnumerable<string> Labels { get; set; }
}
