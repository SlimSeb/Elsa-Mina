using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.UrlPreview;

public class UrlPreviewViewModel : LocalizableViewModel
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; }
    public string ImageUrl { get; init; }
    public string SiteName { get; init; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}
