using CommunityToolkit.Mvvm.ComponentModel;
using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Gui.Pages.Room;

public partial class RoomParameterViewModel : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    private string _value = "-";

    public RoomParameterViewModel(Parameter parameter, IParameterDefinition definition)
    {
        Name = FormatName(parameter.ToString());
        Value = definition.DefaultValue;
    }

    private static string FormatName(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
