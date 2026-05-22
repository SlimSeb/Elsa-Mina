using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Misc.RandomImages;

public class TenorGifViewModel : LocalizableViewModel
{
    public required string Url { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
