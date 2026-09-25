namespace ElsaMina.Core.Services.Templates;

public interface ITemplatesManager
{
    void LoadTemplates();
    Task<string> GetTemplateAsync(string templateKey, object model);
}
