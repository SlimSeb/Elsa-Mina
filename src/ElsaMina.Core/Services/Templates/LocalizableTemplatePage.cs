using ElsaMina.Core.Services.Resources;
using Microsoft.AspNetCore.Components;

namespace ElsaMina.Core.Services.Templates;

public abstract class LocalizableTemplatePage<TViewModel> : TemplatePage<TViewModel>
    where TViewModel : LocalizableViewModel
{
    [Inject]
    private IResourcesService ResourcesService { get; set; }

    protected string GetString(string key, params object[] formatArguments)
    {
        return string.Format(ResourcesService.GetString(key, Model.Culture), formatArguments);
    }
}
