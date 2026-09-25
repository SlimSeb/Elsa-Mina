using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Resources;

namespace ElsaMina.Core.Services.Templates;

public abstract class LocalizableTemplatePage<TViewModel> : TemplatePage<TViewModel>
    where TViewModel : LocalizableViewModel
{
    private readonly Lazy<IResourcesService> _resourceService =
        new(() => DependencyContainerService.Current.Resolve<IResourcesService>());

    protected string GetString(string key, params object[] formatArguments)
    {
        return string.Format(_resourceService.Value.GetString(key, Model.Culture), formatArguments);
    }
}
