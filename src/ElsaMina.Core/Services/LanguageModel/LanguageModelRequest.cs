namespace ElsaMina.Core.Services.LanguageModel;

public class LanguageModelRequest
{
    public string SystemPrompt { get; set; }
    public List<LanguageModelMessage> InputConversation { get; set; }

    public override string ToString()
    {
        return $"SYSTEM={SystemPrompt} CONVO={string.Join(" ", InputConversation?.Select(x => $"{x.Role}: {x.Content}") ?? [])}";
    }
}
