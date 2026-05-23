using System.Reflection;
using ElsaMina.Core.Services.Config;

namespace ElsaMina.Gui.Services.Version;

public class VersionProvider : IVersionProvider
{
    public string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
