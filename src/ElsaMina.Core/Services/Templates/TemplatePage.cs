using Microsoft.AspNetCore.Components;

namespace ElsaMina.Core.Services.Templates;

public abstract class TemplatePage<TModel> : ComponentBase
{
    [Parameter]
    public TModel Model { get; set; }

    protected static MarkupString Raw(string html)
    {
        return new MarkupString(html);
    }
}
