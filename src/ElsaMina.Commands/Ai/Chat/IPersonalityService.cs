namespace ElsaMina.Commands.Ai.Chat;

public interface IPersonalityService
{
    BotPersonality GetPersonality(string roomId);
    void SetPersonality(string roomId, BotPersonality personality);
}