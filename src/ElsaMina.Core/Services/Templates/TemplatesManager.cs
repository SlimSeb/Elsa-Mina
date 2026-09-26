using System.Collections.Concurrent;
using System.Reflection;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElsaMina.Core.Services.Templates;

public class TemplatesManager : ITemplatesManager
{
    private const string TEMPLATES_NAMESPACE = "ElsaMina.Templates";

    private const string ASSEMBLY_NAME_PREFIX = "ElsaMina.";
    private const string MODEL_PARAMETER_NAME = nameof(TemplatePage<>.Model);

    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, Type> _templateTypes = new();

    public TemplatesManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void LoadTemplates()
    {
        var templateTypes = GetElsaMinaAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsTemplateType);

        foreach (var templateType in templateTypes)
        {
            _templateTypes[GetTemplateKey(templateType)] = templateType;
        }

        Log.Information("Loaded {0} templates", _templateTypes.Count);
    }

    /// <summary>
    /// Returns the loaded ElsaMina assemblies plus the ElsaMina assemblies they reference, so templates are
    /// found even when their assembly has not been loaded yet.
    /// </summary>
    private static IEnumerable<Assembly> GetElsaMinaAssemblies()
    {
        var assembliesByName = new Dictionary<string, Assembly>();
        var pendingAssemblies = new Queue<Assembly>(AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => IsElsaMinaAssembly(assembly.GetName())));

        while (pendingAssemblies.TryDequeue(out var assembly))
        {
            if (!assembliesByName.TryAdd(assembly.GetName().Name!, assembly))
            {
                continue;
            }

            foreach (var referencedName in assembly.GetReferencedAssemblies().Where(IsElsaMinaAssembly))
            {
                if (!assembliesByName.ContainsKey(referencedName.Name!))
                {
                    pendingAssemblies.Enqueue(Assembly.Load(referencedName));
                }
            }
        }

        return assembliesByName.Values;
    }

    private static bool IsElsaMinaAssembly(AssemblyName assemblyName)
    {
        return assemblyName.Name?.StartsWith(ASSEMBLY_NAME_PREFIX) == true;
    }

    public async Task<string> GetTemplateAsync(string templateKey, object model)
    {
        if (!_templateTypes.TryGetValue(templateKey, out var templateType))
        {
            return null;
        }

        // A renderer keeps every component it renders alive until it is disposed, so each render gets its own
        await using var htmlRenderer = new HtmlRenderer(_serviceProvider, NullLoggerFactory.Instance);
        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [MODEL_PARAMETER_NAME] = model
            });
            var output = await htmlRenderer.RenderComponentAsync(templateType, parameters);
            return output.ToHtmlString();
        });

        return html.RemoveNewlines();
    }

    private static bool IsTemplateType(Type type)
    {
        return !type.IsAbstract
               && typeof(IComponent).IsAssignableFrom(type)
               && type.Namespace != null
               && (type.Namespace == TEMPLATES_NAMESPACE || type.Namespace.StartsWith(TEMPLATES_NAMESPACE + "."));
    }

    private static string GetTemplateKey(Type templateType)
    {
        var relativeNamespace = templateType.Namespace!.Substring(TEMPLATES_NAMESPACE.Length).TrimStart('.');
        return relativeNamespace.Length == 0
            ? templateType.Name
            : $"{relativeNamespace.Replace('.', '/')}/{templateType.Name}";
    }
}
