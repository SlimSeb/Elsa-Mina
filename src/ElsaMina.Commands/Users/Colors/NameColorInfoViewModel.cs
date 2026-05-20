using System.Drawing;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Users.Colors;

public class NameColorInfoViewModel : LocalizableViewModel
{
    public string Name { get; init; }
    public Color Color { get; init; }
    public Color? OriginalColor { get; init; }
}