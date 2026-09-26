using ElsaMina.Core.Services.CustomColors;
using Microsoft.AspNetCore.Components;

namespace ElsaMina.Core.Services.Templates;

public abstract class TemplatePage<TModel> : ComponentBase
{
    [Parameter]
    public TModel Model { get; set; }

    [Inject]
    private IUserColorsService UserColorsService { get; set; }

    protected static MarkupString Raw(string html)
    {
        return new MarkupString(html);
    }

    protected string GetUserColor(string userName)
    {
        return userName == null ? null : UserColorsService.GetUserColor(userName);
    }
}
