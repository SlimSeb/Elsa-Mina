using System.Collections.Concurrent;

namespace ElsaMina.Commands.Ai.Chat;

/// <summary>
/// In-memory, per-room store of the currently selected chat personality.
/// Rooms without an explicit choice fall back to <see cref="BotPersonalities.DEFAULT"/>.
/// </summary>
public sealed class PersonalityService : IPersonalityService
{
    private readonly ConcurrentDictionary<string, BotPersonality> _personalitiesByRoom = new();

    public BotPersonality GetPersonality(string roomId)
        => _personalitiesByRoom.TryGetValue(roomId ?? string.Empty, out var personality)
            ? personality
            : BotPersonalities.DEFAULT;

    public void SetPersonality(string roomId, BotPersonality personality)
        => _personalitiesByRoom[roomId ?? string.Empty] = personality;
}
