using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ElsaMina.DataAccess.Models;

/// <summary>
/// A user-owned, saved deck for the simplified Pokémon TCG. Card ids and energy types are persisted
/// as JSON strings; the <see cref="NotMappedAttribute"/> helpers expose them as typed lists.
/// </summary>
[Table("TcgDecks")]
public class TcgDeck
{
    public string Id { get; set; }

    public string OwnerId { get; set; }
    public SavedUser Owner { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// JSON-serialized list of card ids (with duplicates for multiple copies).
    /// </summary>
    public string CardsJson { get; set; } = "[]";

    /// <summary>
    /// JSON-serialized list of the deck's Energy Zone types (stored as their string names).
    /// </summary>
    public string EnergyTypesJson { get; set; } = "[]";

    public DateTime CreationDate { get; set; }

    [NotMapped]
    public List<string> Cards
    {
        get => string.IsNullOrWhiteSpace(CardsJson)
            ? []
            : JsonSerializer.Deserialize<List<string>>(CardsJson) ?? [];
        set => CardsJson = JsonSerializer.Serialize(value ?? []);
    }

    [NotMapped]
    public List<string> EnergyTypes
    {
        get => string.IsNullOrWhiteSpace(EnergyTypesJson)
            ? []
            : JsonSerializer.Deserialize<List<string>>(EnergyTypesJson) ?? [];
        set => EnergyTypesJson = JsonSerializer.Serialize(value ?? []);
    }
}
