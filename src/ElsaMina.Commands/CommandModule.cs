using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Autofac;
using ElsaMina.Commands.Modules;
using Assembly = System.Reflection.Assembly;

namespace ElsaMina.Commands;

public partial class CommandModule : Module
{
    private static readonly Regex FEATURE_RESOURCE_PATTERN =
        FeatureResourcePatternRegex();

    private static IEnumerable<ResourceManager> DiscoverFeatureResources()
    {
        var probeCulture = new CultureInfo("en-US");
        var seen = new HashSet<(string BaseName, Assembly Assembly)>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.FullName?.StartsWith("ElsaMina.") != true)
            {
                continue;
            }

            Assembly satelliteAssembly;
            try
            {
                satelliteAssembly = assembly.GetSatelliteAssembly(probeCulture);
            }
            catch
            {
                continue;
            }

            foreach (var resourceName in satelliteAssembly.GetManifestResourceNames())
            {
                var match = FEATURE_RESOURCE_PATTERN.Match(resourceName);
                if (match.Success)
                {
                    seen.Add((match.Groups[1].Value, assembly));
                }
            }
        }

        return seen.Select(item => new ResourceManager(item.BaseName, item.Assembly));
    }

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        foreach (var resourceManager in DiscoverFeatureResources())
        {
            builder.RegisterInstance(resourceManager).As<ResourceManager>().SingleInstance();
        }

        builder.RegisterModule<AdminModule>();
        builder.RegisterModule<AiModule>();
        builder.RegisterModule<ArcadeModule>();
        builder.RegisterModule<BadgesModule>();
        builder.RegisterModule<ProfileModule>();
        builder.RegisterModule<GamesModule>();
        builder.RegisterModule<MiscModule>();
        builder.RegisterModule<ShowdownModule>();
        builder.RegisterModule<TeamsModule>();
        builder.RegisterModule<TournamentsModule>();
        builder.RegisterModule<UsersModule>();
        builder.RegisterModule<RoomsModule>();
    }

    [GeneratedRegex(@"^(.+\.Resources\.\w+?)\.[\w-]+\.resources$", RegexOptions.Compiled)]
    private static partial Regex FeatureResourcePatternRegex();
}
