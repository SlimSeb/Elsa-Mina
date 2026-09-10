using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Games.GuessingGame.PokeDesc;

public class PokemonDescription
{
    public string EnglishName { get; set; }
    public string FrenchName { get; set; }
    public string Description { get; set; }
}